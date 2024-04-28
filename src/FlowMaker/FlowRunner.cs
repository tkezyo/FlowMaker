using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker;

public class FlowRunner : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowManager _flowManager;
    private readonly IFlowProvider _flowProvider;
    private readonly FlowMakerOption _flowMakerOption;
    /// <summary>
    /// 全局上下文
    /// </summary>
    public FlowContext Context { get; set; } = null!;
    /// <summary>
    /// 执行步骤的事件
    /// </summary>
    private Subject<ExecuteStepEvent> ExecuteStepSubject { get; } = new();
    /// <summary>
    /// 锁,防止多线程执行步骤分发
    /// </summary>
    private readonly Subject<Unit> _locker = new();
    private CancellationToken _cancellationToken;
    /// <summary>
    /// 流程Id
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// 释放资源
    /// </summary>
    private CompositeDisposable Disposables { get; set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option, FlowManager flowManager, IFlowProvider flowProvider)
    {
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
        this._flowManager = flowManager;
        this._flowProvider = flowProvider;
        var d = ExecuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(c =>
          {
              var key = c.Type + c.Type switch
              {
                  EventType.PreStep => c.StepId?.ToString(),
                  EventType.Event => c.EventName,
                  EventType.StartFlow => "",
                  _ => ""
              };
              if (Context.ExecuteStepIds.TryGetValue(key, out var steps))
              {
                  foreach (var item in steps)
                  {
                      var step = Context.FlowDefinition.Steps.First(c => c.Id == item);

                      Context.StepState[item].Waits.RemoveAll(c => c == key);

                      if (Context.StepState[item].Waits.Count == 0)
                      {
                          _ = Run(step, _cancellationToken);
                      }
                  }
              }
              if (Context.StepState.All(c => c.Value.EndTime.HasValue) && State == FlowState.Running)
              {
                  //全部完成
                  if (TaskCompletionSource is not null)
                  {
                      FlowResult flowResult = new();
                      flowResult.Success = true;
                      flowResult.CurrentIndex = Context.CurrentIndex;
                      flowResult.ErrorIndex = Context.ErrorIndex;

                      foreach (var item in Context.FlowDefinition.Data)
                      {
                          if (!item.IsOutput)
                          {
                              continue;
                          }
                          if (Context.Data.TryGetValue(item.Name, out var data))
                          {
                              flowResult.Data.Add(new FlowResultData { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value });
                          }
                      }
                      if (!Context.Finally)
                      {
                          State = FlowState.Complete;
                          TaskCompletionSource.SetResult(flowResult);
                      }
                      else
                      {
                          flowResult.Success = false;
                          State = FlowState.Error;
                          TaskCompletionSource.SetException(new StepOnFinallyException(flowResult));
                      }

                  }
              }

              _locker.OnNext(Unit.Default);
          });
        Disposables.Add(d);
    }


    protected TaskCompletionSource<FlowResult>? TaskCompletionSource { get; set; }
    /// <summary>
    /// 流程状态
    /// </summary>
    public FlowState State { get; protected set; } = FlowState.None;

    /// <summary>
    /// 子流程执行器
    /// </summary>
    protected Dictionary<Guid, FlowRunner> SubFlowRunners { get; set; } = [];
    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="step"></param>
    /// <param name="stepContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task RunStep(FlowStep step, StepContext stepContext, CancellationToken cancellationToken)
    {
        if (step.Type == StepType.Normal)
        {
            var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name)
                ?? throw new Exception($"未找到{step.Category}，{step.Name}定义");

            var stepObj = _serviceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(stepContext, _serviceProvider, cancellationToken);
        }
        else if (step.Type == StepType.Embedded)
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);
            var embeddedFlow = subFlowDefinition.EmbeddedFlows.First(c => c.StepId == step.Id);
            var flowRunner = _serviceProvider.GetRequiredService<FlowRunner>();

            var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
            flowRunner.Id = step.Id;
            SubFlowRunners.Add(step.Id, flowRunner);
            config.Middlewares = Context.Middlewares;

            var results = await flowRunner.Start(embeddedFlow, subFlowDefinition.Checkers, config, Context.FlowIds, stepContext.StepOnceStatus.CurrentIndex, stepContext.StepOnceStatus.ErrorIndex, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
        }
        else
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);
            var flowRunner = _serviceProvider.GetRequiredService<FlowRunner>();

            var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = await IDataConverterInject.GetValue(step.Inputs.First(v => v.Name == item.Name), _serviceProvider, Context, item.DefaultValue, cancellationToken);
                config.Data.Add(new NameValue(item.Name, value));
            }
            flowRunner.Id = step.Id;
            SubFlowRunners.Add(step.Id, flowRunner);
            config.Middlewares = Context.Middlewares;

            var results = await flowRunner.Start(subFlowDefinition, subFlowDefinition.Checkers, config, Context.FlowIds, stepContext.StepOnceStatus.CurrentIndex, stepContext.StepOnceStatus.ErrorIndex, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
        }
    }
    /// <summary>
    /// 检查步骤是否需要执行
    /// </summary>
    /// <param name="flowStep"></param>
    /// <param name="convertId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected async Task<(bool, string)> CheckStep(FlowStep flowStep, Guid convertId, CancellationToken cancellationToken)
    {
        var checker = Context.Checkers.FirstOrDefault(c => c.Id == convertId) ?? flowStep.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
        var result = await IDataConverterInject.GetValue(checker, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
        return (result, checker.Name);
    }
    /// <summary>
    /// 开始执行流程
    /// </summary>
    /// <param name="flowInfo"></param>
    /// <param name="config"></param>
    /// <param name="parentIds"></param>
    /// <param name="currentIndex"></param>
    /// <param name="errorIndex"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<FlowResult> Start(IFlowDefinition flowInfo, List<FlowInput> checkers, ConfigDefinition config, Guid[] parentIds, int currentIndex, int errorIndex, CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is null)
        {
            _cancellationToken = CancellationTokenSource.Token;
        }
        else
        {
            _cancellationToken = cancellationToken.Value;
        }
        if (State != FlowState.None && State != FlowState.Complete && State != FlowState.Error)
        {
            throw new Exception("正在运行中");
        }
        State = FlowState.Running;

        try
        {
            TaskCompletionSource = new TaskCompletionSource<FlowResult>();

            Context = new(flowInfo, checkers, config, [.. parentIds, Id], currentIndex, errorIndex);

            Context.InitState();

            foreach (var item in _flowMakerOption.DefaultMiddlewares)
            {
                Context.Middlewares.Add(item.Value);
            }

            foreach (var item in config.Middlewares)
            {
                Context.Middlewares.Add(item);
            }
            Context.Middlewares = Context.Middlewares.Distinct().ToList();


            foreach (var item in Context.Middlewares)
            {
                _flowMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IFlowMiddleware>(item));
            }
            foreach (var item in Context.Middlewares)
            {
                _eventMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IEventMiddleware>(item));
            }

            foreach (var item in Context.Middlewares)
            {
                _stepMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IStepMiddleware>(item));
            }

            foreach (var item in Context.Middlewares)
            {
                _stepOnceMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IStepOnceMiddleware>(item));
            }

            foreach (var item in Context.Middlewares)
            {
                _logMiddlewares.AddRange(_serviceProvider.GetKeyedServices<ILogMiddleware>(item));
            }


            foreach (var item in flowInfo.Data)//写入 globe data
            {
                if (!item.IsInput)
                {
                    continue;
                }
                var value = config.Data.FirstOrDefault(c => c.Name == item.Name);
                if (value is null)
                {
                    continue;
                }
                if (Context.Data.TryGetValue(item.Name, out var data))
                {
                    data.Value = value.Value;
                }
                else
                {
                    Context.Data[item.Name] = new FlowGlobeData(item.Name, item.Type, value.Value);
                }
            }


            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuting(Context, State, CancellationTokenSource.Token);
            }

            ExecuteStepSubject.OnNext(new ExecuteStepEvent
            {
                Type = EventType.StartFlow,
            });
            var result = await TaskCompletionSource.Task;

            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuted(Context, State, null, CancellationTokenSource.Token);
            }

            return result;
        }
        catch (Exception e)
        {
            State = FlowState.Error;

            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuted(Context, State, e, CancellationTokenSource.Token);
            }
            throw;
        }
    }

    /// <summary>
    /// 发送事件中间件
    /// </summary>
    protected readonly List<IEventMiddleware> _eventMiddlewares = [];
    protected readonly List<IStepMiddleware> _stepMiddlewares = [];
    protected readonly List<IStepOnceMiddleware> _stepOnceMiddlewares = [];
    protected readonly List<ILogMiddleware> _logMiddlewares = [];
    protected readonly List<IFlowMiddleware> _flowMiddlewares = [];
    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public async Task SendEventAsync(string eventName, string? eventData)
    {
        foreach (var item in _eventMiddlewares)
        {
            await item.OnExecuting(Context, eventName, eventData, CancellationTokenSource.Token);
        }

        Context.EventData[eventName] = eventData;
        ExecuteStepSubject.OnNext(new ExecuteStepEvent
        {
            Type = EventType.Event,
            EventData = eventData,
            EventName = eventName
        });

        foreach (var item in SubFlowRunners)
        {
            await item.Value.SendEventAsync(eventName, eventData);
        }
    }
    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="step"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        Context.StepState[step.Id].StartTime = DateTime.Now;
        Context.StepState[step.Id].State = StepState.Start;

        try
        {
            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuting(Context, step, Context.StepState[step.Id], CancellationTokenSource.Token);
            }
            var repeat = await IDataConverterInject.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            var isFinally = await IDataConverterInject.GetValue(step.Finally, _serviceProvider, Context, s => bool.TryParse(s, out var r) ? r : false, cancellationToken);
            int errorIndex = 0;
            bool skip = false;
            bool success = true;
            for (int i = 1; i <= repeat; i++)//重复执行
            {
                foreach (var item2 in step.Ifs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    (bool result, string reason) = await CheckStep(step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        StepOnceStatus once = new(i, errorIndex);
                        once.State = StepOnceState.Skip;

                        Context.StepState[step.Id].OnceStatuses.Add(once);
                        await Log(step, Context.StepState[step.Id], once, "Skip Reason: " + reason, LogLevel.Information, CancellationTokenSource.Token);

                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuting(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                        }
                        skip = true;
                        break;
                    }
                }

                if (Context.Finally && !isFinally)
                {
                    StepOnceStatus once = new(i, errorIndex);
                    once.State = StepOnceState.Skip;

                    Context.StepState[step.Id].OnceStatuses.Add(once);
                    await Log(step, Context.StepState[step.Id], once, "Skip Reason: Finally", LogLevel.Information, CancellationTokenSource.Token);

                    foreach (var item in _stepOnceMiddlewares)
                    {
                        await item.OnExecuting(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                    }
                    skip = true;
                }
                if (skip)
                {
                    break;
                }
                while (true)
                {
                    StepOnceStatus once = new(i, errorIndex);
                    Context.StepState[step.Id].OnceStatuses.Add(once);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        once.StartTime = DateTime.Now;
                        once.State = StepOnceState.Start;
                        var timeOut = await IDataConverterInject.GetValue(step.TimeOut, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuting(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                        }
                        StepContext stepContext = new(step, Context, Context.StepState[step.Id], once, async (log, logLevel) =>
                        {
                            if (CancellationTokenSource.IsCancellationRequested) { return; }
                            await Log(step, Context.StepState[step.Id], once, log, logLevel, CancellationTokenSource.Token);
                        });

                        //超时策略
                        if (timeOut > 0)
                        {
                            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(timeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
                            await timeoutPolicy.ExecuteAsync(async c => await RunStep(step, stepContext, c), cancellationToken);
                        }
                        else
                        {
                            await RunStep(step, stepContext, cancellationToken);
                        }
                        once.EndTime = DateTime.Now;
                        once.State = StepOnceState.Complete;
                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuted(Context, step, Context.StepState[step.Id], once, null, CancellationTokenSource.Token);
                        }

                        break;
                    }
                    catch (Exception e)
                    {
                        var retry = await IDataConverterInject.GetValue(step.Retry, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);

                        errorIndex++;
                        once.EndTime = DateTime.Now;
                        once.State = StepOnceState.Error;

                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuted(Context, step, Context.StepState[step.Id], once, e, CancellationTokenSource.Token);
                        }

                        if (retry >= errorIndex)
                        {
                            continue;
                        }

                        var errorHandling = await IDataConverterInject.GetValue(step.ErrorHandling, _serviceProvider, Context, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);
                        success = false;
                        switch (errorHandling)
                        {
                            case ErrorHandling.Skip:
                                break;
                            case ErrorHandling.Finally:
                                Context.Finally = true;
                                break;
                            case ErrorHandling.Terminate:
                                Context.StepState[step.Id].State = StepState.Error;
                                Context.StepState[step.Id].EndTime = DateTime.Now;
                                foreach (var item in _stepMiddlewares)
                                {
                                    await item.OnExecuted(Context, step, Context.StepState[step.Id], null, CancellationTokenSource.Token);
                                }
                                TaskCompletionSource?.SetException(e);
                                return;
                            default:
                                break;
                        }

                        break;
                    }
                }
            }
            Context.StepState[step.Id].EndTime = DateTime.Now;
            if (success)
            {
                Context.StepState[step.Id].State = StepState.Complete;
            }
            else
            {
                Context.StepState[step.Id].State = StepState.Error;
            }
            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(Context, step, Context.StepState[step.Id], null, CancellationTokenSource.Token);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            //执行下一步
            ExecuteStepSubject.OnNext(new ExecuteStepEvent
            {
                Type = EventType.PreStep,
                StepId = step.Id,
            });
        }
        catch (Exception e)
        {
            TaskCompletionSource?.SetException(e);
        }
    }
    /// <summary>
    /// 停止流程
    /// </summary>
    /// <returns></returns>
    public async Task StopAsync()
    {
        if (State != FlowState.Running)
        {
            return;
        }

        foreach (var middleware in _flowMiddlewares)
        {
            await middleware.OnExecuted(Context, State, null, CancellationTokenSource.Token);
        }
        foreach (var item in SubFlowRunners)
        {
            await item.Value.StopAsync();
        }
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        State = FlowState.Cancel;

        TaskCompletionSource?.SetCanceled();
    }
    public void Dispose()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        CancellationTokenSource.Dispose();
        State = FlowState.Complete;
        Disposables.Dispose();
    }

    public async Task Log(FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information, CancellationToken cancellationToken = default)
    {
        DateTime dateTime = DateTime.Now;
        foreach (var item in _logMiddlewares)
        {
            await item.Log(Context, flowStep, stepStatus, stepOnceStatus, dateTime, log, logLevel, cancellationToken);
        }
    }
}


[Serializable]
public class StepOnFinallyException : Exception
{
    public FlowResult Result { get; set; }
    public StepOnFinallyException(FlowResult flowResult)
    {
        Result = flowResult;
    }
    public StepOnFinallyException(string message, FlowResult flowResult) : base(message)
    {
        Result = flowResult;
    }
    public StepOnFinallyException(string message, Exception inner, FlowResult flowResult) : base(message, inner)
    {
        Result = flowResult;
    }

}