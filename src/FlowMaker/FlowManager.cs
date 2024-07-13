using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
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

    public async IAsyncEnumerable<FlowResult> Run(string configName, string flowCategory, string flowName)
    {
        var config = await _flowProvider.LoadConfigDefinitionAsync(flowCategory, flowName, configName) ?? throw new InvalidOperationException("未找到配置");
        var id = await Init(config, false);
        await foreach (var item in Run(id))
        {
            yield return item;
        }
    }



    public async IAsyncEnumerable<FlowResult> Run(Guid id)
    {
        var status = _status[id];
        if (!status.Runners.TryGetValue(id.ToString(), out var runner))
        {
            yield break;
        }
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
                        flowResult = await timeoutPolicy.ExecuteAsync(async () => await runner.Start(status, flowContext, status.Cancel.Token));
                    }
                    else
                    {
                        flowResult = await runner.Start(status, flowContext, status.Cancel.Token);
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
            await status.SendEventAsync(eventName, eventData);
        }
    }
    public async Task Stop(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.Dispose();

            status.Cancel.Cancel();
        }
        await Task.CompletedTask;
    }

    public async Task Dispose(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.Dispose();

            _status.Remove(id, out _);
        }
        await Task.CompletedTask;
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
        }
        catch (Exception e)
        {


        }

        return default;
    }
    #endregion

    #region SingleRun

 
    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];
    public async Task<Guid> Init(ConfigDefinition configDefinition, bool singleRun)
    {
        Guid id = Guid.NewGuid();
        var scope = _serviceProvider.CreateScope();
        _status[id] = new RunnerStatus(configDefinition, scope)
        {
            Cancel = new(),
            SingleRun = singleRun
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

            if (!singleRun)
            {
                return;
            }
            foreach (var step in context.FlowDefinition.Steps)
            {
                if (step.Type == StepType.Embedded)
                {
                    var subFlowDefinition = await _flowProvider.LoadFlowDefinitionAsync(step.Category, step.Name);
                    var embeddedFlow = subFlowDefinition.EmbeddedFlows.First(c => c.StepId == step.Id);

                    var config = new ConfigDefinition { ConfigName = null, Category = step.Category, Name = step.Name };
                    config.Middlewares = context.Middlewares;

                    FlowContext subContext = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. context.FlowIds, step.Id], 1, 0, context.Index, context.Logs, context.WaitEvents, context.Data);

                    _status[id].Contexts.TryAdd(string.Join("", subContext.FlowIds), subContext);

                    FlowRunner flowRunningStatus = new FlowRunner(scope.ServiceProvider, _flowMakerOption, _flowProvider);
                    _status[id].Runners.TryAdd(string.Join("", subContext.FlowIds), flowRunningStatus);


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
                    _status[id].Contexts.TryAdd(string.Join("", subContext.FlowIds), subContext);

                    FlowRunner flowRunningStatus = new FlowRunner(scope.ServiceProvider, _flowMakerOption, _flowProvider);
                    _status[id].Runners.TryAdd(string.Join("", subContext.FlowIds), flowRunningStatus);
                }
            }
        }


        await SetContextAsync(flowContext);

        FlowRunner runner = new FlowRunner(scope.ServiceProvider, _flowMakerOption, _flowProvider);
        _status[id].Runners.TryAdd(string.Join("", flowContext.FlowIds), runner);

        _status[id].Contexts.TryAdd(string.Join("", flowContext.FlowIds), flowContext);

        foreach (var item in _flowMakerOption.DefaultMiddlewares)
        {
            if (!flowContext.Middlewares.Any(c => c == item.Value))
            {
                flowContext.Middlewares.Add(item.Value);
            }
        }

        foreach (var item in flowContext.Middlewares)
        {
            _status[id].FlowMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IFlowMiddleware>(item));
        }
        foreach (var item in flowContext.Middlewares)
        {
            _status[id].EventMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IEventMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _status[id].StepMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IStepMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _status[id].StepOnceMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<IStepOnceMiddleware>(item));
        }

        foreach (var item in flowContext.Middlewares)
        {
            _status[id].LogMiddlewares.AddRange(scope.ServiceProvider.GetKeyedServices<ILogMiddleware>(item));
        }


        await Task.CompletedTask;
        return id;
    }

    public async Task ExecuteSingleFlow(Guid id)
    {
        var status = _status[id];
        if (!status.Contexts.TryGetValue(id.ToString(), out var flowContext))
        {
            return;
        }
        if (!status.Runners.TryGetValue(id.ToString(), out var runner))
        {
            return;
        }
        await runner.StartSingleFlow(status, flowContext, default);
    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunStep(Guid[] ids, FlowStep flowStep, CancellationToken cancellationToken)
    {
        var status = _status[ids[0]];
        if (!status.Contexts.TryGetValue(string.Join("", ids), out var flowContext))
        {
            return;
        }
        if (!status.Runners.TryGetValue(string.Join("", ids), out var runner))
        {
            return;
        }
        await runner.RunSingleStep(flowContext, flowStep, cancellationToken);
    }

    #endregion
}
public class RunnerStatus(ConfigDefinition config, IServiceScope serviceScope) : IDisposable
{
    public ConfigDefinition Config { get; set; } = config;

    public bool SingleRun { get; set; }
    public bool Running { get; set; }

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

    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public async Task SendEventAsync(string eventName, string? eventData)
    {
        foreach (var item in Contexts)
        {
            foreach (var eventMiddleware in EventMiddlewares)
            {
                await eventMiddleware.OnExecuting(item.Value, eventName, eventData, Cancel.Token);
            }

            await Runners[item.Key].ExecuteEventAsync(item.Value, eventName, eventData);
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