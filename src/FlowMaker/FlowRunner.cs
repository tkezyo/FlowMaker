using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Generic;
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

    private Subject<ExecuteStep> ExecuteStepSubject { get; } = new();
    private readonly Subject<Unit> _locker = new();
    private CancellationToken _cancellationToken;
    public Guid Id { get; set; } = Guid.NewGuid();
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
              if (Context.StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
              {
                  //全部完成
                  if (TaskCompletionSource is not null)
                  {
                      List<FlowResult> results = [];
                      foreach (var item in Context.FlowDefinition.Data)
                      {
                          if (!item.IsOutput)
                          {
                              continue;
                          }
                          if (Context.Data.TryGetValue(item.Name, out var data))
                          {
                              results.Add(new FlowResult { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value });
                          }
                      }
                      State = RunnerState.Complete;
                      TaskCompletionSource.SetResult(results);
                  }
              }

              _locker.OnNext(Unit.Default);
          });
        Disposables.Add(d);
    }


    protected TaskCompletionSource<List<FlowResult>>? TaskCompletionSource { get; set; }
    public RunnerState State { get; protected set; } = RunnerState.None;


    private Dictionary<Guid, FlowRunner> SubFlowRunners { get; set; } = [];
    protected async Task RunStep(FlowStep step, StepContext stepContext, CancellationToken cancellationToken)
    {
        var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name);
        if (stepDefinition is not null)
        {
            var stepObj = _serviceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(Context, stepContext, _serviceProvider, cancellationToken);
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

            var results = await flowRunner.Start(subFlowDefinition, config, Context.FlowIds, cancellationToken);

            foreach (var item in results)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
        }
    }
    protected async Task<bool> CheckStep(FlowStep flowStep, Guid convertId, CancellationToken cancellationToken)
    {
        var converter = Context.FlowDefinition.Checkers.FirstOrDefault(c => c.Id == convertId) ?? flowStep.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
        return await IDataConverterInject.GetValue(converter, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
    }
    public async Task<List<FlowResult>> Start(FlowDefinition flowInfo, ConfigDefinition config, Guid[] parentIds, CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is null)
        {
            _cancellationToken = CancellationTokenSource.Token;
        }
        else
        {
            _cancellationToken = cancellationToken.Value;
        }
        if (State != RunnerState.None)
        {
            throw new Exception("正在运行中");
        }
        State = RunnerState.Running;
        List<IFlowMiddleware> middlewares = [];

        try
        {
            TaskCompletionSource = new TaskCompletionSource<List<FlowResult>>();

            Context = new(flowInfo, config, [.. parentIds, Id]);

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
                middlewares.AddRange(_serviceProvider.GetKeyedServices<IFlowMiddleware>(item));
            }
            foreach (var item in Context.Middlewares)
            {
                _eventMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IEventMiddleware>(item));
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


            foreach (var middleware in middlewares)
            {
                await middleware.OnExecuting(Context, State, CancellationTokenSource.Token);
            }

            ExecuteStepSubject.OnNext(new ExecuteStep
            {
                Type = EventType.StartFlow,
            });
            var result = await TaskCompletionSource.Task;

            foreach (var middleware in middlewares)
            {
                await middleware.OnExecuted(Context, State, CancellationTokenSource.Token);
            }

            return result;
        }
        catch (Exception e)
        {
            State = RunnerState.Error;

            foreach (var middleware in middlewares)
            {
                await middleware.OnError(Context, State, e, CancellationTokenSource.Token);
            }
            throw;
        }
    }

    private readonly List<IEventMiddleware> _eventMiddlewares = [];

    public async Task SendEventAsync(string eventName, string? eventData)
    {
        foreach (var item in _eventMiddlewares)
        {
            await item.OnExecuting(Context, eventName, eventData, CancellationTokenSource.Token);
        }

        Context.EventData[eventName] = eventData;
        ExecuteStepSubject.OnNext(new ExecuteStep
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

    protected async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        List<IStepMiddleware> stepMiddlewares = [];
        List<IStepOnceMiddleware> stepOnceMiddlewares = [];

        foreach (var item in Context.Middlewares)
        {
            stepMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IStepMiddleware>(item));
        }

        foreach (var item in Context.Middlewares)
        {
            stepOnceMiddlewares.AddRange(_serviceProvider.GetKeyedServices<IStepOnceMiddleware>(item));
        }

        try
        {
            foreach (var item in stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuting(Context, step, Context.StepState[step.Id], CancellationTokenSource.Token);
            }
            var repeat = await IDataConverterInject.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            int errorIndex = 0;
            bool skip = false;
            for (int i = 0; i < repeat; i++)//重复执行
            {
                foreach (var item2 in step.Ifs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    var result = await CheckStep(step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        StepOnceStatus once = new(i, errorIndex);
                        once.State = StepOnceState.Skip;
                        Context.StepState[step.Id].OnceStatuses.Add(once);
                        foreach (var item in stepOnceMiddlewares)
                        {
                            await item.OnExecuting(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                        }
                        skip = true;
                        break;
                    }
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
                        foreach (var item in stepOnceMiddlewares)
                        {
                            await item.OnExecuting(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                        }
                        //超时策略
                        if (timeOut > 0)
                        {
                            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(timeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
                            await timeoutPolicy.ExecuteAsync(async c => await RunStep(step, new StepContext(step, Context.StepState[step.Id], once), c), cancellationToken);
                        }
                        else
                        {
                            await RunStep(step, new StepContext(step, Context.StepState[step.Id], once), cancellationToken);
                        }
                        once.EndTime = DateTime.Now;
                        once.State = StepOnceState.Complete;
                        foreach (var item in stepOnceMiddlewares)
                        {
                            await item.OnExecuted(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        errorIndex++;


                        var retry = await IDataConverterInject.GetValue(step.Retry, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
                        if (retry < errorIndex)
                        {
                            break;
                        }

                        var errorHandling = await IDataConverterInject.GetValue(step.ErrorHandling, _serviceProvider, Context, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);

                        switch (errorHandling)
                        {
                            case ErrorHandling.Skip:
                                once.EndTime = DateTime.Now;
                                once.State = StepOnceState.Complete;
                                foreach (var item in stepOnceMiddlewares)
                                {
                                    await item.OnExecuted(Context, step, Context.StepState[step.Id], once, CancellationTokenSource.Token);
                                }
                                break;
                            case ErrorHandling.Suspend:
                                Context.StepState[step.Id].Suspend = true;
                                return;
                            case ErrorHandling.Terminate:
                                once.EndTime = DateTime.Now;
                                once.State = StepOnceState.Error;
                                foreach (var item in stepOnceMiddlewares)
                                {
                                    await item.OnError(Context, step, Context.StepState[step.Id], once, e, CancellationTokenSource.Token);
                                }
                                TaskCompletionSource?.SetException(e);
                                return;
                            default:
                                break;
                        }
                    }
                }

            }

            Context.StepState[step.Id].Complete = true;
            Context.StepState[step.Id].EndTime = DateTime.Now;
            foreach (var item in stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(Context, step, Context.StepState[step.Id], CancellationTokenSource.Token);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            //执行下一步
            ExecuteStepSubject.OnNext(new ExecuteStep
            {
                Type = EventType.PreStep,
                StepId = step.Id,
            });
        }

        catch (Exception e)
        {
            foreach (var item in stepMiddlewares)
            {
                await item.OnError(Context, step, Context.StepState[step.Id], e, CancellationTokenSource.Token);
            }
            TaskCompletionSource?.SetException(e);
        }
    }

    public async Task StopAsync()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        State = RunnerState.Cancel;

        List<IFlowMiddleware> middlewares = [];
        foreach (var item in Context.Middlewares)
        {
            middlewares.AddRange(_serviceProvider.GetKeyedServices<IFlowMiddleware>(item));
        }
        foreach (var middleware in middlewares)
        {
            await middleware.OnExecuted(Context, State, CancellationTokenSource.Token);
        }
    }
    public void Dispose()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        CancellationTokenSource.Dispose();
        State = RunnerState.Complete;
        Disposables.Dispose();
    }
}

public enum StepState
{
    Start,
    Complete,
    ReceivedEvent,
    ReceivedEventData,
    ReceivedCancelDebug,
}
public enum StepOnceState
{
    Start,
    Complete,
    Error,
    Skip
}
/// <summary>
/// 事件
/// </summary>
public class ExecuteStep
{
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
    public string? EventData { get; set; }
}
/// <summary>
/// 事件类型
/// </summary>
public enum EventType
{
    PreStep,
    Event,
    StartFlow,
}
/// <summary>
/// 步骤运行状态
/// </summary>
public enum RunnerState
{
    None,
    Running,
    Complete,
    Cancel,
    Error,
}
