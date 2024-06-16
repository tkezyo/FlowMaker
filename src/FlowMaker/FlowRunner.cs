﻿using DynamicData;
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
    /// 释放资源
    /// </summary>
    private CompositeDisposable Disposables { get; set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();


    public FlowRunner(IServiceProvider serviceProvider, FlowMakerOption option, IFlowProvider flowProvider)
    {
        _flowMakerOption = option;
        this._serviceProvider = serviceProvider;
        this._flowProvider = flowProvider;
        var d = ExecuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(ExecuteStep);
        Disposables.Add(d);
    }

    protected void ExecuteStep(ExecuteStepEvent executeStep)
    {
        var key = executeStep.Type + executeStep.Type switch
        {
            EventType.PreStep => executeStep.StepId?.ToString(),
            EventType.Event => executeStep.EventName,
            EventType.StartFlow => "",
            _ => ""
        };
        if (Context.ExecuteStepIds.TryGetValue(key, out var steps))
        {
            foreach (var item in steps)
            {
                var stepState = Context.StepState.Lookup(item);
                if (stepState.HasValue)
                {
                    stepState.Value.Waits.Remove(key);

                    if (stepState.Value.Waits.Count == 0)
                    {
                        var step = Context.FlowDefinition.Steps.First(c => c.Id == item);
                        _ = Run(step, _cancellationToken);
                    }
                }
            }
        }

        if (Context.StepState.Items.All(c => c.EndTime.HasValue) && Context.State == FlowState.Running)
        {
            //全部完成
            if (TaskCompletionSource is not null)
            {
                FlowResult flowResult = new()
                {
                    Success = true,
                    CurrentIndex = Context.CurrentIndex,
                    ErrorIndex = Context.ErrorIndex
                };

                foreach (var item in Context.FlowDefinition.Data)
                {
                    if (!item.IsOutput)
                    {
                        continue;
                    }
                    var data = Context.Data.Lookup(item.Name);
                    if (data.HasValue)
                    {
                        flowResult.Data.Add(new FlowResultData { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value.Value });
                    }
                }
                if (!Context.Finally)
                {
                    Context.State = FlowState.Complete;
                    TaskCompletionSource.SetResult(flowResult);
                }
                else
                {
                    flowResult.Success = false;
                    Context.State = FlowState.Error;
                    TaskCompletionSource.SetException(new StepOnFinallyException(flowResult));
                }

            }
        }

        _locker.OnNext(Unit.Default);
    }

    protected TaskCompletionSource<FlowResult>? TaskCompletionSource { get; set; }


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
            var flowRunner = new FlowRunner(_serviceProvider, _flowMakerOption, _flowProvider);

            var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
            SubFlowRunners.Add(step.Id, flowRunner);
            config.Middlewares = Context.Middlewares;

            FlowContext context = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. Context.FlowIds, step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, Context.Index, Context.Logs, Context.WaitEvents, Context.Data);
            var stepState = Context.StepState.Lookup(step.Id);
            if (stepState.HasValue)
            {
                stepState.Value.FlowContext = context;
            }
            var results = await flowRunner.Start(context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
            SubFlowRunners.Remove(step.Id);
            flowRunner.Dispose();
        }
        else
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);
            var flowRunner = new FlowRunner(_serviceProvider, _flowMakerOption, _flowProvider);

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
            FlowContext context = new(subFlowDefinition, config, subFlowDefinition.Checkers, [.. Context.FlowIds, step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, Context.Index, Context.Logs, Context.WaitEvents);
            var stepState = Context.StepState.Lookup(step.Id);
            if (stepState.HasValue)
            {
                stepState.Value.FlowContext = context;
            }
            var results = await flowRunner.Start(context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
            SubFlowRunners.Remove(step.Id);
            flowRunner.Dispose();
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
    /// <param name="checkers"></param>
    /// <param name="flowContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<FlowResult> Start(FlowContext flowContext, CancellationToken? cancellationToken = null)
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
            TaskCompletionSource = new TaskCompletionSource<FlowResult>();

            Context = flowContext;

            Context.Init();

            if (Context.State != FlowState.Wait && Context.State != FlowState.Complete && Context.State != FlowState.Error)
            {
                throw new Exception("正在运行中");
            }
            Context.State = FlowState.Running;

            _flowMiddlewares.Clear();
            _eventMiddlewares.Clear();
            _stepMiddlewares.Clear();
            _stepOnceMiddlewares.Clear();
            _logMiddlewares.Clear();


            foreach (var item in _flowMakerOption.DefaultMiddlewares)
            {
                if (!Context.Middlewares.Any(c => c == item.Value))
                {
                    Context.Middlewares.Add(item.Value);
                }
            }

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

            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuting(Context, CancellationTokenSource.Token);
            }

            ExecuteStepSubject.OnNext(new ExecuteStepEvent
            {
                Type = EventType.StartFlow,
            });
            var result = await TaskCompletionSource.Task;
            Context.EndTime = DateTime.Now;
            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuted(Context, null, CancellationTokenSource.Token);
            }
            return result;
        }
        catch (Exception e)
        {
            Context.State = FlowState.Error;
            Context.EndTime = DateTime.Now;

            foreach (var middleware in _flowMiddlewares)
            {
                await middleware.OnExecuted(Context, e, CancellationTokenSource.Token);
            }
            throw;
        }
        finally
        {
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

        Context.WaitEvents.RemoveKey(eventName);

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
        var stepState = Context.StepState.Lookup(step.Id);
        if (!stepState.HasValue)
        {
            throw new Exception("执行过程中，配置步骤错误");
        }
        stepState.Value.StartTime = DateTime.Now;
        stepState.Value.State = StepState.Start;
        var repeat = await IDataConverterInject.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
        var retry = await IDataConverterInject.GetValue(step.Retry, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
        var isFinally = await IDataConverterInject.GetValue(step.Finally, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
        var errorHandling = await IDataConverterInject.GetValue(step.ErrorHandling, _serviceProvider, Context, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);
        stepState.Value.Repeat = repeat;
        stepState.Value.Retry = retry;
        stepState.Value.Finally = isFinally;
        stepState.Value.ErrorHandling = errorHandling;
        Context.StepState.AddOrUpdate(stepState.Value);

        try
        {
            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuting(Context, step, stepState.Value, CancellationTokenSource.Token);
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
                    (bool result, string reason) = await CheckStep(step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        skipReason = reason;
                        state = StepState.Skip;
                        break;
                    }
                }

                if (Context.Finally && !isFinally)
                {
                    skipReason = "Finally";

                    state = StepState.Skip;
                }
                async Task Log(StepOnceStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information)
                {
                    var info = new LogInfo(log, logLevel, DateTime.Now, step.Id, stepOnceStatus.Index);
                    Context.Logs.Add(info);
                    foreach (var item in _logMiddlewares)
                    {
                        await item.OnLog(Context, step, stepState.Value, stepOnceStatus, info, CancellationTokenSource.Token);
                    }
                }
                if (state == StepState.Skip)
                {
                    StepOnceStatus once = new(i, errorIndex, Context.Index, Log)
                    {
                        State = StepOnceState.Skip
                    };

                    stepState.Value.OnceLogs.AddOrUpdate(once);

                    await Log(once, "Skip Reason: " + skipReason);

                    foreach (var item in _stepOnceMiddlewares)
                    {
                        await item.OnExecuting(Context, step, stepState.Value, once, CancellationTokenSource.Token);
                    }
                    break;
                }
                while (true)
                {
                    StepOnceStatus once = new(i, errorIndex, Context.Index, Log);

                    List<string> additionalConditions = [];
                    once.ExtraData.Add(StepOnceStatus.AdditionalConditions, additionalConditions);
                    foreach (var item2 in step.AdditionalConditions)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        (bool result, string reason) = await CheckStep(step, item2.Key, cancellationToken);
                        if (result == item2.Value)
                        {
                            additionalConditions.Add(reason);
                        }
                    }

                    StepContext stepContext = new(step, Context, once);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        once.StartTime = DateTime.Now;
                        once.State = StepOnceState.Start;
                        stepState.Value.OnceLogs.AddOrUpdate(once);
                        var timeOut = await IDataConverterInject.GetValue(step.TimeOut, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuting(Context, step, stepState.Value, once, CancellationTokenSource.Token);
                        }

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
                        stepState.Value.OnceLogs.AddOrUpdate(once);

                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuted(Context, step, stepState.Value, once, null, CancellationTokenSource.Token);
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

                        foreach (var item in _stepOnceMiddlewares)
                        {
                            await item.OnExecuted(Context, step, stepState.Value, once, e, CancellationTokenSource.Token);
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
                                Context.Finally = true;
                                break;
                            case ErrorHandling.Terminate:
                                stepState.Value.State = StepState.Error;
                                stepState.Value.EndTime = DateTime.Now;
                                foreach (var item in _stepMiddlewares)
                                {
                                    await item.OnExecuted(Context, step, stepState.Value, null, CancellationTokenSource.Token);
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
            Context.StepState.AddOrUpdate(stepState.Value);

            foreach (var item in _stepMiddlewares)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await item.OnExecuted(Context, step, stepState.Value, null, CancellationTokenSource.Token);
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
        if (Context.State != FlowState.Running)
        {
            return;
        }
        Context.State = FlowState.Cancel;

        foreach (var middleware in _flowMiddlewares)
        {
            await middleware.OnExecuted(Context, null, CancellationTokenSource.Token);
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

        TaskCompletionSource?.SetCanceled();
    }
    public void Dispose()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        foreach (var item in SubFlowRunners)
        {
            item.Value.Dispose();
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