using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ty;

namespace FlowMaker;

public class FlowRunner : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFlowProvider _flowProvider;
    private readonly FlowMakerOption _flowMakerOption;

    /// <summary>
    /// 执行步骤的事件
    /// </summary>
    private Subject<ExecuteStepEvent> ExecuteStepSubject { get; } = new();
    /// <summary>
    /// 锁,防止多线程执行步骤分发
    /// </summary>
    private readonly Subject<Unit> _locker = new();
    private CancellationToken _cancellationToken { get; set; }

    /// <summary>
    /// 释放资源
    /// </summary>
    private CompositeDisposable Disposables { get; set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    protected TaskCompletionSource<FlowResult>? TaskCompletionSource { get; set; }
    public RunnerStatus? RunnerStatus { get; set; }

    public FlowRunner(IServiceProvider serviceProvider, FlowMakerOption option, IFlowProvider flowProvider)
    {
        _flowMakerOption = option;
        this._serviceProvider = serviceProvider;
        this._flowProvider = flowProvider;
        var d = ExecuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(ExecuteNextStep);
        Disposables.Add(d);
    }

    /// <summary>
    /// 触发事件后,执行关联步骤
    /// </summary>
    /// <param name="executeStep"></param>
    protected void ExecuteNextStep(ExecuteStepEvent executeStep)
    {
        var key = executeStep.Type + executeStep.Type switch
        {
            EventType.PreStep => executeStep.StepId?.ToString(),
            EventType.Event => executeStep.EventName,
            EventType.StartFlow => "",
            _ => ""
        };
        if (executeStep.Context.ExecuteStepIds.TryGetValue(key, out var steps))
        {
            foreach (var item in steps)
            {
                var stepState = executeStep.Context.StepState.Lookup(item);
                if (stepState.HasValue)
                {
                    stepState.Value.Waits.Remove(key);

                    if (stepState.Value.Waits.Count == 0)
                    {
                        var step = executeStep.Context.FlowDefinition.Steps.First(c => c.Id == item);
                        _ = Run(executeStep.Context, step, _cancellationToken);
                    }
                }
            }
        }

        if (executeStep.Context.StepState.Items.All(c => c.EndTime.HasValue) && executeStep.Context.State == FlowState.Running)
        {
            //全部完成
            if (TaskCompletionSource is not null)
            {
                FlowResult flowResult = new()
                {
                    Success = true,
                    CurrentIndex = executeStep.Context.CurrentIndex,
                    ErrorIndex = executeStep.Context.ErrorIndex
                };

                foreach (var item in executeStep.Context.FlowDefinition.Data)
                {
                    if (!item.IsOutput)
                    {
                        continue;
                    }
                    var data = executeStep.Context.Data.Lookup(item.Name);
                    if (data.HasValue)
                    {
                        flowResult.Data.Add(new FlowResultData { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value.Value });
                    }
                }
                if (!executeStep.Context.Finally)
                {
                    executeStep.Context.State = FlowState.Complete;
                    TaskCompletionSource.SetResult(flowResult);
                }
                else
                {
                    flowResult.Success = false;
                    executeStep.Context.State = FlowState.Error;
                    TaskCompletionSource.SetException(new StepOnFinallyException(flowResult));
                }

            }
        }

        _locker.OnNext(Unit.Default);
    }
    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public async Task ExecuteEventAsync(FlowContext flowContext, string eventName, string? eventData)
    {
        if (RunnerStatus is null)
        {
            return;
        }

        flowContext.EventData[eventName] = eventData;
        ExecuteStepSubject.OnNext(new ExecuteStepEvent
        {
            Type = EventType.Event,
            EventData = eventData,
            EventName = eventName,
            Context = flowContext
        });

        flowContext.WaitEvents.RemoveKey(eventName);

        flowContext.EventLogs.Add(new EventLog
        {
            EventName = eventName,
            EventData = eventData,
            Time = DateTime.Now
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="stepContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task ExecuteStep(StepContext stepContext, CancellationToken cancellationToken)
    {
        if (RunnerStatus is null)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (stepContext.Step.Type == StepType.Normal)
        {
            var stepDefinition = _flowMakerOption.GetStep(stepContext.Step.Category, stepContext.Step.Name)
                ?? throw new Exception($"未找到{stepContext.Step.Category}，{stepContext.Step.Name}定义");

            var stepObj = _serviceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(stepContext, _serviceProvider, cancellationToken);
        }
        else if (stepContext.Step.Type == StepType.Embedded)
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(stepContext.Step.Category, stepContext.Step.Name);
            var embeddedFlow = subFlowDefinition.EmbeddedFlows.First(c => c.StepId == stepContext.Step.Id);
            var flowRunner = new FlowRunner(_serviceProvider, _flowMakerOption, _flowProvider);

            var config = new ConfigDefinition { ConfigName = null, Category = stepContext.Step.Category, Name = stepContext.Step.Name };


            FlowContext context = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. stepContext.FlowContext.FlowIds, stepContext.Step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, stepContext.FlowContext.Index, stepContext.FlowContext.Logs, stepContext.FlowContext.WaitEvents, stepContext.FlowContext.Data);

            if (!RunnerStatus.SingleRun)
            {
                RunnerStatus.Contexts.TryAdd(context.Index, context);
            }
            var results = await flowRunner.Start(RunnerStatus, context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, stepContext.FlowContext, cancellationToken);
            }
            flowRunner.Dispose();
            if (!RunnerStatus.SingleRun)
            {
                RunnerStatus.Contexts.TryRemove(context.Index, out _);
            }

        }
        else
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(stepContext.Step.Category, stepContext.Step.Name);
            var flowRunner = new FlowRunner(_serviceProvider, _flowMakerOption, _flowProvider);

            var config = new ConfigDefinition { ConfigName = null, Category = stepContext.Step.Category, Name = stepContext.Step.Name };
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = await IDataConverterInject.GetValue(stepContext.Step.Inputs.First(v => v.Name == item.Name), _serviceProvider, stepContext.FlowContext, item.DefaultValue, cancellationToken);
                config.Data.Add(new NameValue(item.Name, value));
            }
            config.Middlewares = stepContext.FlowContext.Middlewares;
            FlowContext context = new(subFlowDefinition, config, subFlowDefinition.Checkers, [.. stepContext.FlowContext.FlowIds, stepContext.Step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, stepContext.FlowContext.Index, stepContext.FlowContext.Logs, stepContext.FlowContext.WaitEvents);

            if (!RunnerStatus.SingleRun)
            {
                RunnerStatus.Contexts.TryAdd(context.Index, context);
            }

            var results = await flowRunner.Start(RunnerStatus, context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, stepContext.FlowContext, cancellationToken);
            }
            flowRunner.Dispose();
            if (!RunnerStatus.SingleRun)
            {
                RunnerStatus.Contexts.TryRemove(context.Index, out _);
            }
        }
    }
    /// <summary>
    /// 检查步骤是否需要执行
    /// </summary>
    /// <param name="flowContext"></param>
    /// <param name="flowStep"></param>
    /// <param name="convertId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    protected async Task<(bool, string)> CheckStep(FlowContext flowContext, FlowStep flowStep, Guid convertId, CancellationToken cancellationToken)
    {
        var checker = flowContext.Checkers.FirstOrDefault(c => c.Id == convertId) ?? flowStep.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
        var result = await IDataConverterInject.GetValue(checker, _serviceProvider, flowContext, s => bool.TryParse(s, out var r) && r, cancellationToken);
        return (result, checker.Name);
    }
    /// <summary>
    /// 开始执行流程
    /// </summary>
    /// <param name="runnerStatus"></param>
    /// <param name="flowContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<FlowResult> Start(RunnerStatus runnerStatus, FlowContext flowContext, CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is null)
        {
            _cancellationToken = CancellationTokenSource.Token;
        }
        else
        {
            _cancellationToken = cancellationToken.Value;
        }

        try
        {
            RunnerStatus = runnerStatus;
            TaskCompletionSource = new TaskCompletionSource<FlowResult>();


            flowContext.Init();

            if (flowContext.State != FlowState.Wait && flowContext.State != FlowState.Complete && flowContext.State != FlowState.Error)
            {
                throw new Exception("正在运行中");
            }
            flowContext.State = FlowState.Running;

            foreach (var middleware in RunnerStatus.FlowMiddlewares)
            {
                await middleware.OnExecuting(flowContext, CancellationTokenSource.Token);
            }

            ExecuteStepSubject.OnNext(new ExecuteStepEvent
            {
                Type = EventType.StartFlow,
                Context = flowContext
            });
            var result = await TaskCompletionSource.Task;
            flowContext.EndTime = DateTime.Now;
            foreach (var middleware in RunnerStatus.FlowMiddlewares)
            {
                await middleware.OnExecuted(flowContext, null, CancellationTokenSource.Token);
            }
            return result;
        }
        catch (Exception e)
        {
            flowContext.State = FlowState.Error;
            flowContext.EndTime = DateTime.Now;

            if (RunnerStatus is not null)
            {
                foreach (var middleware in RunnerStatus.FlowMiddlewares)
                {
                    await middleware.OnExecuted(flowContext, e, CancellationTokenSource.Token);
                }
            }

            throw;
        }
        finally
        {
        }
    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="step"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task Run(FlowContext flowContext, FlowStep step, CancellationToken cancellationToken)
    {
        if (RunnerStatus is null)
        {
            return;
        }
        var stepState = flowContext.StepState.Lookup(step.Id);
        if (!stepState.HasValue)
        {
            throw new Exception("执行过程中，配置步骤错误");
        }
        stepState.Value.StartTime = DateTime.Now;
        stepState.Value.State = StepState.Start;
        var repeat = await IDataConverterInject.GetValue(step.Repeat, _serviceProvider, flowContext, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
        var retry = await IDataConverterInject.GetValue(step.Retry, _serviceProvider, flowContext, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
        var isFinally = await IDataConverterInject.GetValue(step.Finally, _serviceProvider, flowContext, s => bool.TryParse(s, out var r) && r, cancellationToken);
        var errorHandling = await IDataConverterInject.GetValue(step.ErrorHandling, _serviceProvider, flowContext, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);
        stepState.Value.Repeat = repeat;
        stepState.Value.Retry = retry;
        stepState.Value.Finally = isFinally;
        stepState.Value.ErrorHandling = errorHandling;
        flowContext.StepState.AddOrUpdate(stepState.Value);

        try
        {
            foreach (var item in RunnerStatus.StepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuting(flowContext, step, stepState.Value, CancellationTokenSource.Token);
            }

            int errorIndex = 0;
            StepState state = StepState.Start;
            for (int i = 1; i <= repeat; i++)//重复执行
            {
                string? skipReason = null;
                foreach (var item2 in step.Ifs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    (bool result, string reason) = await CheckStep(flowContext, step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        skipReason = reason;
                        state = StepState.Skip;
                        break;
                    }
                }

                if (flowContext.Finally && !isFinally)
                {
                    skipReason = "Finally";

                    state = StepState.Skip;
                }
                async Task Log(StepOnceStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information)
                {
                    var info = new LogInfo(log, logLevel, DateTime.Now, step.Id, stepOnceStatus.Index);
                    flowContext.Logs.Add(info);
                    foreach (var item in RunnerStatus.LogMiddlewares)
                    {
                        await item.OnLog(flowContext, step, stepState.Value, stepOnceStatus, info, CancellationTokenSource.Token);
                    }
                }
                if (state == StepState.Skip)
                {
                    StepOnceStatus once = new(i, errorIndex, flowContext.Index, Log)
                    {
                        State = StepOnceState.Skip
                    };

                    stepState.Value.OnceLogs.AddOrUpdate(once);

                    await Log(once, "Skip Reason: " + skipReason);

                    foreach (var item in RunnerStatus.StepOnceMiddlewares)
                    {
                        await item.OnExecuting(flowContext, step, stepState.Value, once, CancellationTokenSource.Token);
                    }
                    break;
                }
                while (true)
                {
                    StepOnceStatus once = new(i, errorIndex, flowContext.Index, Log);

                    List<string> additionalConditions = [];
                    once.ExtraData.Add(StepOnceStatus.AdditionalConditions, additionalConditions);
                    foreach (var item2 in step.AdditionalConditions)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        (bool result, string reason) = await CheckStep(flowContext, step, item2.Key, cancellationToken);
                        if (result == item2.Value)
                        {
                            additionalConditions.Add(reason);
                        }
                    }

                    StepContext stepContext = new(step, flowContext, once);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        once.StartTime = DateTime.Now;
                        once.State = StepOnceState.Start;
                        stepState.Value.OnceLogs.AddOrUpdate(once);
                        var timeOut = await IDataConverterInject.GetValue(step.TimeOut, _serviceProvider, flowContext, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
                        foreach (var item in RunnerStatus.StepOnceMiddlewares)
                        {
                            await item.OnExecuting(flowContext, step, stepState.Value, once, CancellationTokenSource.Token);
                        }

                        //超时策略
                        if (timeOut > 0)
                        {
                            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(timeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
                            await timeoutPolicy.ExecuteAsync(async c => await ExecuteStep(stepContext, c), cancellationToken);
                        }
                        else
                        {
                            await ExecuteStep(stepContext, cancellationToken);
                        }
                        once.EndTime = DateTime.Now;
                        once.State = StepOnceState.Complete;
                        stepState.Value.OnceLogs.AddOrUpdate(once);

                        foreach (var item in RunnerStatus.StepOnceMiddlewares)
                        {
                            await item.OnExecuted(flowContext, step, stepState.Value, once, null, CancellationTokenSource.Token);
                        }
                        state = StepState.Complete;

                        break;
                    }
                    catch (Exception e)
                    {
                        errorIndex++;
                        once.EndTime = DateTime.Now;
                        once.State = StepOnceState.Error;
                        stepState.Value.OnceLogs.AddOrUpdate(once);
                        await stepContext.Log(e.Message, LogLevel.Error);

                        foreach (var item in RunnerStatus.StepOnceMiddlewares)
                        {
                            await item.OnExecuted(flowContext, step, stepState.Value, once, e, CancellationTokenSource.Token);
                        }

                        if (retry >= errorIndex)
                        {
                            continue;
                        }

                        state = StepState.Error;
                        switch (errorHandling)
                        {
                            case ErrorHandling.Skip:
                                break;
                            case ErrorHandling.Finally:
                                flowContext.Finally = true;
                                break;
                            case ErrorHandling.Terminate:
                                stepState.Value.State = StepState.Error;
                                stepState.Value.EndTime = DateTime.Now;
                                foreach (var item in RunnerStatus.StepMiddlewares)
                                {
                                    await item.OnExecuted(flowContext, step, stepState.Value, null, CancellationTokenSource.Token);
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
            stepState.Value.EndTime = DateTime.Now;
            stepState.Value.State = state;
            flowContext.StepState.AddOrUpdate(stepState.Value);

            foreach (var item in RunnerStatus.StepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(flowContext, step, stepState.Value, null, CancellationTokenSource.Token);
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
                Context = flowContext
            });
        }
        catch (Exception e)
        {
            TaskCompletionSource?.SetException(e);
        }
    }


    public async Task StartSingleFlow(RunnerStatus runnerStatus, FlowContext flowContext, CancellationToken? cancellationToken = null)
    {
        RunnerStatus = runnerStatus;

        flowContext.State = FlowState.Running;
        foreach (var item in RunnerStatus.FlowMiddlewares)
        {
            await item.OnExecuting(flowContext, cancellationToken ?? default);
        }
    }
    public async Task RunSingleStep(FlowContext flowContext, FlowStep flowStep, CancellationToken cancellationToken)
    {
        if (RunnerStatus is null)
        {
            return;
        }

        var status = RunnerStatus;

        var stepState = flowContext.StepState.Lookup(flowStep.Id);
        if (!stepState.HasValue)
        {
            throw new Exception("未找到步骤信息");
        }
        var stepOnce = new StepOnceStatus(1, 0, flowContext.Index, async (stepOnceStatus, log, level) =>
        {
            await status.Log(flowContext.FlowIds, flowStep, stepState.Value, stepOnceStatus, log, level);
        });
        stepOnce.StartTime = DateTime.Now;
        stepOnce.State = StepOnceState.Start;
        foreach (var item in status.StepMiddlewares)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            await item.OnExecuting(flowContext, flowStep, stepState.Value, cancellationToken);
        }

        foreach (var item in status.StepOnceMiddlewares)
        {
            await item.OnExecuting(flowContext, flowStep, stepState.Value, stepOnce, cancellationToken);
        }
        StepContext stepContext = new(flowStep, flowContext, stepOnce);

        try
        {
            await ExecuteStep(stepContext, cancellationToken);
            foreach (var item in status.StepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(flowContext, flowStep, stepState.Value, null, cancellationToken);
            }
            stepOnce.EndTime = DateTime.Now;
            stepOnce.State = StepOnceState.Complete;

            foreach (var item in status.StepOnceMiddlewares)
            {
                await item.OnExecuted(flowContext, flowStep, stepState.Value, stepOnce, null, cancellationToken);
            }
        }
        catch (Exception e)
        {
            foreach (var item in status.StepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(flowContext, flowStep, stepState.Value, e, cancellationToken);
            }
            stepOnce.EndTime = DateTime.Now;
            stepOnce.State = StepOnceState.Error;

            foreach (var item in status.StepOnceMiddlewares)
            {
                await item.OnExecuted(flowContext, flowStep, stepState.Value, stepOnce, e, cancellationToken);
            }
        }


    }

    /// <summary>
    /// 停止流程
    /// </summary>
    /// <returns></returns>
    public async Task StopAsync(FlowContext flowContext)
    {
        if (flowContext.State != FlowState.Running || RunnerStatus is null)
        {
            return;
        }
        flowContext.State = FlowState.Cancel;

        foreach (var middleware in RunnerStatus.FlowMiddlewares)
        {
            await middleware.OnExecuted(flowContext, null, CancellationTokenSource.Token);
        }

        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

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