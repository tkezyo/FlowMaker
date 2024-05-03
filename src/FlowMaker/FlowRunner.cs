using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker;

public class FlowRunner : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFlowProvider _flowProvider;
    private readonly FlowMakerOption _flowMakerOption;
    /// <summary>
    /// 全局上下文
    /// </summary>
    public FlowContext Context { get; set; } = null!;
    /// <summary>
    /// 检查项
    /// </summary>
    public List<FlowInput> Checkers { get; set; } = [];
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
    /// 释放资源
    /// </summary>
    private CompositeDisposable Disposables { get; set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();

    /// <summary>
    /// 流程配置
    /// </summary>
    public IFlowDefinition FlowDefinition { get; set; } = null!;
    /// <summary>
    /// 触发某事件时需要执行的Step
    /// </summary>
    public Dictionary<string, List<Guid>> ExecuteStepIds { get; } = [];

    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option, IFlowProvider flowProvider)
    {
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
        this._flowProvider = flowProvider;
        ExecuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(c =>
           {
               var key = c.Type + c.Type switch
               {
                   EventType.PreStep => c.StepId?.ToString(),
                   EventType.Event => c.EventName,
                   EventType.StartFlow => "",
                   _ => ""
               };
               if (ExecuteStepIds.TryGetValue(key, out var steps))
               {
                   foreach (var item in steps)
                   {
                       if (Context.StepState.TryGetValue(item, out var stepState))
                       {
                           stepState.Waits.Remove(key);

                           if (stepState.Waits.Count == 0)
                           {
                               var step = FlowDefinition.Steps.First(c => c.Id == item);
                               _ = Run(step, _cancellationToken);
                           }
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

                       foreach (var item in FlowDefinition.Data)
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
           }).DisposeWith(Disposables);
    }


    protected TaskCompletionSource<FlowResult>? TaskCompletionSource { get; set; }
    /// <summary>
    /// 流程状态
    /// </summary>
    public FlowState State { get; protected set; } = FlowState.Wait;

    /// <summary>
    /// 子流程执行器
    /// </summary>
    protected Dictionary<Guid, FlowRunner> SubFlowRunners { get; set; } = [];

    public void InitExecuteStepIds()
    {
        ExecuteStepIds.Clear();
        if (FlowDefinition is EmbeddedFlowDefinition embeddedFlowDefinition)
        {
            //遍历子流程，如果是串行执行，则在他的WaitEvents中添加依赖的流程Id，如果是并行执行，则在他的WaitEvents中添加上一个串行流程Id
            FlowStep? last = null;
            foreach (var item in FlowDefinition.Steps)
            {
                if (last is not null)
                {
                    item.WaitEvents.Add(new FlowEvent
                    {
                        Type = EventType.PreStep,
                        StepId = last?.Id
                    });
                }
                if (!item.Parallel)
                {
                    last = item;
                }
            }
        }

        foreach (var item in FlowDefinition.Steps)
        {
            List<string> waitEvent = [];

            void Register(FlowInput flowInput)
            {
                if (flowInput.Mode == InputMode.Event)
                {
                    var key = flowInput.Mode + flowInput.Value;
                    waitEvent.Add(key);
                    if (!ExecuteStepIds.TryGetValue(key, out var list))
                    {
                        list = [];
                        ExecuteStepIds.Add(key, list);
                    }
                    list.Add(item.Id);
                    foreach (var subInput in flowInput.Inputs)
                    {
                        Register(subInput);
                    }
                }
            }
            foreach (var wait in item.Ifs)
            {
                var checker = Checkers.FirstOrDefault(c => c.Id == wait.Key) ?? item.Checkers.FirstOrDefault(c => c.Id == wait.Key);
                if (checker is null)
                {
                    continue;
                }
                Register(checker);
            }
            foreach (var input in item.Inputs)
            {
                Register(input);
            }



            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.PreStep => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.StartFlow => "",
                    _ => ""
                };
                waitEvent.Add(key);

                if (!ExecuteStepIds.TryGetValue(key, out var list))
                {
                    list = [];
                    ExecuteStepIds.Add(key, list);
                }
                list.Add(item.Id);
            }

            if (waitEvent.Count == 0)
            {
                if (!ExecuteStepIds.TryGetValue(EventType.StartFlow.ToString(), out var list))
                {
                    list = [];
                    ExecuteStepIds.Add(EventType.StartFlow.ToString(), list);
                }
                list.Add(item.Id);
                continue;
            }
        }
    }
    /// <summary>
    /// 初始化状态
    /// </summary>
    public void InitState()
    {
        Context.StepState.Clear();
        foreach (var item in FlowDefinition.Steps)
        {
            var state = new StepStatus
            {
                StepId = item.Id
            };

            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.PreStep => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.StartFlow => "",
                    _ => ""
                };
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                state.Waits.Add(key);
            }
            state.Waits = state.Waits.Distinct().ToList();
            Context.StepState.TryAdd(item.Id, state);
        }
    }

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
            SubFlowRunners.Add(step.Id, flowRunner);
            config.Middlewares = Context.Middlewares;

            FlowContext context = new(config, [.. Context.FlowIds, step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex);
            if (Context.StepState.TryGetValue(step.Id, out var stepState))
            {
                stepState.FlowContext = context;
            }
            var results = await flowRunner.Start(embeddedFlow, subFlowDefinition.Checkers, context, cancellationToken);

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
            SubFlowRunners.Add(step.Id, flowRunner);
            config.Middlewares = Context.Middlewares;
            FlowContext context = new(config, [.. Context.FlowIds, step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex);
            if (Context.StepState.TryGetValue(step.Id, out var stepState))
            {
                stepState.FlowContext = context;
            }
            var results = await flowRunner.Start(subFlowDefinition, subFlowDefinition.Checkers, context, cancellationToken);

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
        var checker = Checkers.FirstOrDefault(c => c.Id == convertId) ?? flowStep.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
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
    public async Task<FlowResult> Start(IFlowDefinition flowInfo, List<FlowInput> checkers, FlowContext flowContext, CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is null)
        {
            _cancellationToken = CancellationTokenSource.Token;
        }
        else
        {
            _cancellationToken = cancellationToken.Value;
        }
        if (State != FlowState.Wait && State != FlowState.Complete && State != FlowState.Error)
        {
            throw new Exception("正在运行中");
        }
        State = FlowState.Running;

        try
        {
            TaskCompletionSource = new TaskCompletionSource<FlowResult>();

            FlowDefinition = flowInfo;
            Checkers = checkers;
            Context = flowContext;

            InitExecuteStepIds();
            InitState();

            foreach (var item in _flowMakerOption.DefaultMiddlewares)
            {
                Context.Middlewares.Add(item.Value);
            }

            foreach (var item in Context.ConfigDefinition.Middlewares)
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
                var value = Context.ConfigDefinition.Data.FirstOrDefault(c => c.Name == item.Name);
                var globeData = new FlowGlobeData(item.Name, item.Type, value?.Value);
                globeData.IsInput = item.IsInput;
                globeData.IsOutput = item.IsOutput;
                Context.Data.TryAdd(item.Name, globeData);
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
            Context.EndTime = DateTime.Now;

            return result;
        }
        catch (Exception e)
        {
            State = FlowState.Error;
            Context.EndTime = DateTime.Now;

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
    protected readonly List<IFlowMiddleware> _flowMiddlewares = [];
    protected readonly List<ILogMiddleware> _logMiddlewares = [];
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

        Context.EventLogs.Add(new EventLog
        {
            EventName = eventName,
            EventData = eventData,
            Time = DateTime.Now
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
        if (!Context.StepState.TryGetValue(step.Id, out var stepState))
        {
            throw new Exception("执行过程中，配置步骤错误");
        }
        stepState.StartTime = DateTime.Now;
        stepState.State = StepState.Start;

        try
        {
            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuting(Context, step, stepState, CancellationTokenSource.Token);
            }
            var repeat = await IDataConverterInject.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            var isFinally = await IDataConverterInject.GetValue(step.Finally, _serviceProvider, Context, s => bool.TryParse(s, out var r) ? r : false, cancellationToken);
            int errorIndex = 0;
            bool skip = false;
            bool success = true;
            for (int i = 1; i <= repeat; i++)//重复执行
            {
                string? skipReason = null;
                foreach (var item2 in step.Ifs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    (bool result, string reason) = await CheckStep(step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        skipReason = reason;
                        skip = true;
                        break;
                    }
                }

                if (Context.Finally && !isFinally)
                {
                    skipReason = "Finally";

                    skip = true;
                }
                async Task Log(StepOnceStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information)
                {
                    var info = new LogInfo(log, logLevel, DateTime.Now);
                    stepOnceStatus.Logs.Add(info);
                    foreach (var item in _logMiddlewares)
                    {
                        await item.OnLog(Context, step, stepState, stepOnceStatus, info, CancellationTokenSource.Token);
                    }
                }
                if (skip)
                {
                    StepOnceStatus once = new(i, errorIndex);

                    once.State = StepOnceState.Skip;

                    stepState.OnceLogs.Add(once);

                    await Log(once, "Skip Reason: " + skipReason);

                    foreach (var item in _stepOnceMiddlewares)
                    {
                        await item.OnExecuting(Context, step, stepState, once, CancellationTokenSource.Token);
                    }
                    break;
                }
                while (true)
                {
                    StepOnceStatus once = new(i, errorIndex);

                    stepState.OnceLogs.Add(once);
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
                            await item.OnExecuting(Context, step, stepState, once, CancellationTokenSource.Token);
                        }
                        StepContext stepContext = new(step, Context, once, Log);

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
                            await item.OnExecuted(Context, step, stepState, once, null, CancellationTokenSource.Token);
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
                            await item.OnExecuted(Context, step, stepState, once, e, CancellationTokenSource.Token);
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
                                stepState.State = StepState.Error;
                                stepState.EndTime = DateTime.Now;
                                foreach (var item in _stepMiddlewares)
                                {
                                    await item.OnExecuted(Context, step, stepState, null, CancellationTokenSource.Token);
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
            stepState.EndTime = DateTime.Now;
            if (success)
            {
                stepState.State = StepState.Complete;
            }
            else
            {
                stepState.State = StepState.Error;
            }
            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(Context, step, stepState, null, CancellationTokenSource.Token);
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
            if (item.Value is not null)
            {
                await item.Value.StopAsync();
            }
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
        Disposables.Dispose();
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