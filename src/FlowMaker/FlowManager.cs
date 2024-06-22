﻿using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Threading;
using System.Xml.Linq;
using Ty;

namespace FlowMaker;

/// <summary>
/// 管理所有的流程，包括流程的初始化，运行，停止，事件发送等
/// </summary>
/// <param name="serviceProvider"></param>
/// <param name="flowProvider"></param>
/// <param name="logger"></param>
public class FlowManager(IServiceProvider serviceProvider, IFlowProvider flowProvider, ILogger<FlowManager> logger, IOptions<FlowMakerOption> option)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IFlowProvider _flowProvider = flowProvider;
    private readonly ILogger<FlowManager> _logger = logger;
    private readonly FlowMakerOption _flowMakerOption = option.Value;

    #region Run
    class RunnerStatus(ConfigDefinition config, FlowRunner flowRunner, IServiceScope serviceScope) : IDisposable
    {
        public ConfigDefinition Config { get; set; } = config;

        public IServiceScope ServiceScope { get; set; } = serviceScope;
        public FlowRunner FlowRunner { get; set; } = flowRunner;


        public CancellationTokenSource Cancel { get; set; } = new();

        public bool Disposed { get; set; }
        public void Dispose()
        {
            Disposed = true;

            FlowRunner.Dispose();
            Cancel.Dispose();
            ServiceScope.Dispose();
        }
    }

    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];

    public async IAsyncEnumerable<FlowResult> Run(string configName, string flowCategory, string flowName)
    {
        var config = await _flowProvider.LoadConfigDefinitionAsync(flowCategory, flowName, configName) ?? throw new InvalidOperationException("未找到配置");
        var id = await Init(config);
        await foreach (var item in Run(id))
        {
            yield return item;
        }
    }

    public async Task<Guid> Init(ConfigDefinition configDefinition)
    {
        Guid id = Guid.NewGuid();
        var scope = _serviceProvider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
        var flowProvider = scope.ServiceProvider.GetRequiredService<IFlowProvider>();
        var runner = new FlowRunner(scope.ServiceProvider, options.Value, flowProvider);
        _status[id] = new RunnerStatus(configDefinition, runner, scope)
        {
            Cancel = new CancellationTokenSource()
        };
        await Task.CompletedTask;
        return id;
    }

    public async IAsyncEnumerable<FlowResult> Run(Guid id)
    {
        var status = _status[id];

        var runner = status.FlowRunner;
        var testName = DateTime.Now.ToString("yyyyMMdd") + id;

        var flow = await _flowProvider.LoadFlowDefinitionAsync(status.Config.Category, status.Config.Name);
        if (flow is null)
        {
            _logger.LogError("未找到流程:{TestName}", testName);

            throw new InvalidOperationException("未找到流程");
        }
        _logger.LogInformation("流程开始:{TestName}", testName);
        bool needThrow = false;

        for (int i = 1; i <= status.Config.Repeat || status.Config.Repeat <= 0; i++)
        {
            if (status.Cancel.IsCancellationRequested)
            {
                break;
            }
            if (needThrow)
            {
                break;
            }

            var errorTimes = 0;
            while (!status.Cancel.IsCancellationRequested)
            {
                FlowResult? flowResult = null;
                bool needBreak = false;
                FlowContext flowContext = new(flow, status.Config, flow.Checkers, [id], i, errorTimes, null, null);
                try
                {
                    if (status.Config.Timeout > 0)
                    {
                        var timeoutPolicy = Policy.TimeoutAsync<FlowResult>(TimeSpan.FromSeconds(status.Config.Timeout), Polly.Timeout.TimeoutStrategy.Pessimistic);
                        flowResult = await timeoutPolicy.ExecuteAsync(async () => await runner.Start(flowContext, status.Cancel.Token));
                    }
                    else
                    {
                        flowResult = await runner.Start(flowContext, _status[id].Cancel.Token);
                    }
                    break;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("流程被取消:{TestName}", testName);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "流程出现错误:{TestName}", testName);
                    if (e is StepOnFinallyException finallyException)
                    {
                        flowResult = finallyException.Result;
                    }
                    else
                    {
                        flowResult = new FlowResult
                        {
                            ErrorIndex = errorTimes,
                            Exception = e,
                            CurrentIndex = i,
                            Success = false,
                        };
                    }
                    errorTimes++;

                    if (errorTimes <= status.Config.Retry)
                    {
                        if (status.Config.ErrorStop)
                        {
                            _logger.LogError(e, "流程失败立即停止:{TestName}", testName);
                            needThrow = true;
                        }
                        else
                        {
                            _logger.LogError(e, "流程失败继续:{TestName}", testName);
                        }
                    }
                    else
                    {
                        needBreak = true;
                    }
                }
                finally
                {
                    flowContext.Dispose();
                }
                if (flowResult is not null)
                {
                    yield return flowResult;
                }
                if (needBreak || needThrow)
                {
                    break;
                }
            }
        }
        await Dispose(id);

    }
    public async Task SendEvent(Guid id, string eventName, string? eventData = null)
    {
        if (_status.TryGetValue(id, out var status))
        {
            if (status.FlowRunner is not null)
            {
                await status.FlowRunner.SendEventAsync(eventName, eventData);
            }
        }
    }
    public async Task Stop(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            if (status.FlowRunner is not null)
            {
                await status.FlowRunner.StopAsync();
            }
            status.Cancel.Cancel();
        }
    }

    public async Task Dispose(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.Dispose();
            if (status.FlowRunner is not null)
            {
                while (status.FlowRunner.Context.State == FlowState.Running)
                {
                    await Task.Delay(300);
                }
            }
            _status.Remove(id, out _);
        }
    }

    public Task DisposeAll()
    {
        foreach (var item in _status)
        {
            item.Value.Dispose();
        }
        _status.Clear();
        return Task.CompletedTask;
    }

    public T? GetRunnerService<T>(Guid id, string? key = null)
    {
        try
        {
            if (_status.TryGetValue(id, out var status) && !status.Disposed)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return status.ServiceScope.ServiceProvider.GetService<T>();
                }
                else
                {
                    return status.ServiceScope.ServiceProvider.GetKeyedService<T>(key);
                }
            }

            if (_singleStatus.TryGetValue(id, out var singleStatus) && !singleStatus.Disposed)
            {

                if (string.IsNullOrEmpty(key))
                {
                    return singleStatus.ServiceScope.ServiceProvider.GetService<T>();
                }
                else
                {
                    return singleStatus.ServiceScope.ServiceProvider.GetKeyedService<T>(key);
                }
            }
        }
        catch (Exception e)
        {


        }

        return default;
    }
    #endregion

    #region SingleRun

    public class SingleRunnerStatus(ConfigDefinition config, IServiceScope serviceScope) : IDisposable
    {
        public ConfigDefinition Config { get; set; } = config;

        public IServiceScope ServiceScope { get; set; } = serviceScope;

        public CancellationTokenSource Cancel { get; set; } = new();

        public ConcurrentDictionary<string, FlowContext> Contexts { get; set; } = [];
        public ConcurrentDictionary<string, FlowRunner> Runners { get; set; } = [];

        public List<IEventMiddleware> EventMiddlewares { get; set; } = [];
        public List<IStepMiddleware> StepMiddlewares { get; set; } = [];
        public List<IStepOnceMiddleware> StepOnceMiddlewares { get; set; } = [];
        public List<IFlowMiddleware> FlowMiddlewares { get; set; } = [];
        public List<ILogMiddleware> LogMiddlewares { get; set; } = [];
        public async Task Log(Guid[] ids, FlowStep step, StepStatus stepStatus, StepOnceStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information)
        {
            if (!Contexts.TryGetValue(string.Join("", ids), out var flowContext))
            {
                return;
            }

            var info = new LogInfo(log, logLevel, DateTime.Now, ids.Last(), stepOnceStatus.Index);
            flowContext.Logs.Add(info);
            foreach (var item in LogMiddlewares)
            {
                await item.OnLog(flowContext, step, stepStatus, stepOnceStatus, info, default);
            }
        }
        public bool Disposed { get; set; }
        public void Dispose()
        {
            Disposed = true;

            Cancel.Dispose();
            ServiceScope.Dispose();
        }
    }
    private readonly ConcurrentDictionary<Guid, SingleRunnerStatus> _singleStatus = [];
    public async Task<Guid> InitSingleRun(ConfigDefinition configDefinition)
    {
        Guid id = Guid.NewGuid();
        var scope = _serviceProvider.CreateScope();
        _singleStatus[id] = new SingleRunnerStatus(configDefinition, scope)
        {
            Cancel = new()
        };
        var options = scope.ServiceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
        var flowProvider = scope.ServiceProvider.GetRequiredService<IFlowProvider>();
        var testName = DateTime.Now.ToString("yyyyMMdd") + id;
        var flow = await _flowProvider.LoadFlowDefinitionAsync(configDefinition.Category, configDefinition.Name);
        if (flow is null)
        {
            _logger.LogError("未找到流程:{TestName}", testName);

            throw new InvalidOperationException("未找到流程");
        }

        FlowContext flowContext = new(flow, configDefinition, flow.Checkers, [id], 1, 0, null, null);

        async Task SetContextAsync(FlowContext context)
        {
            context.Init();

            foreach (var step in context.FlowDefinition.Steps)
            {
                if (step.Type == StepType.Embedded)
                {
                    var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);
                    var embeddedFlow = subFlowDefinition.EmbeddedFlows.First(c => c.StepId == step.Id);

                    var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
                    config.Middlewares = context.Middlewares;

                    FlowContext subContext = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. context.FlowIds, step.Id], 1, 0, context.Index, context.Logs, context.WaitEvents, context.Data);

                    _singleStatus[id].Contexts.TryAdd(string.Join("", subContext.FlowIds), subContext);
                }
                else if (step.Type == StepType.SubFlow)
                {
                    var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);

                    var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
                    foreach (var item in subFlowDefinition.Data)
                    {
                        if (!item.IsInput)
                        {
                            continue;
                        }

                        var value = await IDataConverterInject.GetValue(step.Inputs.First(v => v.Name == item.Name), _serviceProvider, context, item.DefaultValue, default);
                        config.Data.Add(new NameValue(item.Name, value));
                    }
                    config.Middlewares = context.Middlewares;
                    FlowContext subContext = new(subFlowDefinition, config, subFlowDefinition.Checkers, [.. context.FlowIds, step.Id], 1, 0, context.Index, context.Logs, context.WaitEvents, context.Data);
                    _singleStatus[id].Contexts.TryAdd(string.Join("", subContext.FlowIds), subContext);
                }
            }
        }


        await SetContextAsync(flowContext);

        _singleStatus[id].Contexts.TryAdd(string.Join("", flowContext.FlowIds), flowContext);

        foreach (var item in _flowMakerOption.DefaultMiddlewares)
        {
            if (!flowContext.Middlewares.Any(c => c == item.Value))
            {
                flowContext.Middlewares.Add(item.Value);
            }
        }

        foreach (var item in flowContext.Middlewares)
        {
            _singleStatus[id].FlowMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IFlowMiddleware>(item));
        }
        foreach (var item in flowContext.Middlewares)
        {
            _singleStatus[id].EventMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IEventMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _singleStatus[id].StepMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IStepMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _singleStatus[id].StepOnceMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IStepOnceMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _singleStatus[id].LogMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<ILogMiddleware>(item));
        }


        await Task.CompletedTask;
        return id;
    }

    public async Task ExecuteSingleFlow(Guid id)
    {
        var status = _singleStatus[id];
        if (!status.Contexts.TryGetValue(string.Join("", id), out var flowContext))
        {
            return;
        }
        flowContext.State = FlowState.Running;
        foreach (var item in status.FlowMiddlewares)
        {
            await item.OnExecuting(flowContext, default);
        }

    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunStep(Guid[] ids, FlowStep flowStep, CancellationToken cancellationToken)
    {
        var status = _singleStatus[ids[0]];
        if (!status.Contexts.TryGetValue(string.Join("", ids), out var flowContext))
        {
            return;
        }
        var stepState = flowContext.StepState.Lookup(flowStep.Id);
        var stepOnce = new StepOnceStatus(1, 0, "", async (stepOnceStatus, log, level) =>
        {
            await status.Log(ids, flowStep, stepState.Value, stepOnceStatus, log, level);
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
        if (stepContext.Step.Type == StepType.Normal)
        {
            var stepDefinition = _flowMakerOption.GetStep(stepContext.Step.Category, stepContext.Step.Name)
                ?? throw new Exception($"未找到{stepContext.Step.Category}，{stepContext.Step.Name}定义");

            var stepObj = status.ServiceScope.ServiceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(stepContext, status.ServiceScope.ServiceProvider, cancellationToken);
        }
        else if (stepContext.Step.Type == StepType.Embedded)
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(stepContext.Step.Category, stepContext.Step.Name);
            var embeddedFlow = subFlowDefinition.EmbeddedFlows.First(c => c.StepId == stepContext.Step.Id);
            var flowRunner = new FlowRunner(status.ServiceScope.ServiceProvider, _flowMakerOption, _flowProvider);

            var config = new ConfigDefinition { ConfigName = null, Category = stepContext.Step.Category, Name = stepContext.Step.Name };

            FlowContext context = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. stepContext.FlowContext.FlowIds, stepContext.Step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, stepContext.FlowContext.Index, stepContext.FlowContext.Logs, stepContext.FlowContext.WaitEvents, stepContext.FlowContext.Data);
            //var stepState = stepContext.FlowContext.StepState.Lookup(stepContext.Step.Id);
            //if (stepState.HasValue)
            //{
            //    stepState.Value.FlowContext = context;
            //}
            var results = await flowRunner.Start(context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v => v.Name == item.Name), item.Value, status.ServiceScope.ServiceProvider, stepContext.FlowContext, cancellationToken);
            }
            flowRunner.Dispose();
        }
        else
        {
            var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(stepContext.Step.Category, stepContext.Step.Name);
            var flowRunner = new FlowRunner(status.ServiceScope.ServiceProvider, _flowMakerOption, _flowProvider);

            var config = new ConfigDefinition { ConfigName = null, Category = stepContext.Step.Category, Name = stepContext.Step.Name };
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = await IDataConverterInject.GetValue(stepContext.Step.Inputs.First(v => v.Name == item.Name), status.ServiceScope.ServiceProvider, stepContext.FlowContext, item.DefaultValue, cancellationToken);
                config.Data.Add(new NameValue(item.Name, value));
            }
            FlowContext context = new(subFlowDefinition, config, subFlowDefinition.Checkers, [.. stepContext.FlowContext.FlowIds, stepContext.Step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, stepContext.FlowContext.Index, stepContext.FlowContext.Logs, stepContext.FlowContext.WaitEvents);
            //var stepState = stepContext.FlowContext.StepState.Lookup(stepContext.Step.Id);
            //if (stepState.HasValue)
            //{
            //    stepState.Value.FlowContext = context;
            //}
            var results = await flowRunner.Start(context, cancellationToken);

            foreach (var item in results.Data)
            {
                await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v => v.Name == item.Name), item.Value, status.ServiceScope.ServiceProvider, stepContext.FlowContext, cancellationToken);
            }
            flowRunner.Dispose();
        }

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

    public void DisposeSingleRun(Guid id)
    {
        if (_singleStatus.TryGetValue(id, out var value))
        {
            value.Dispose();
            _singleStatus.TryRemove(id, out _);
        }
    }

    #endregion
}
