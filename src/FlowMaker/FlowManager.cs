using DynamicData;
using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                bool needBreak = false;
                FlowContext flowContext = new(flow, status.Config, flow.Checkers, [id], i, errorTimes, null, null);
                flowContext.Init();
                _status[id].Contexts.TryAdd(flowContext.Id, flowContext);

                try
                {
                    async Task StartAsync()
                    {
                        var builder = new MiddlewareBuilder<FlowContext>(serviceProvider);

                        foreach (var item in flowContext.FlowMiddlewares)
                        {
                            builder.Use(item);
                        }

                        var application = builder.Build();
                        await application.Invoke(flowContext, status.Cancel.Token);
                    }

                    if (status.Config.Timeout > 0)
                    {
                        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(status.Config.Timeout), Polly.Timeout.TimeoutStrategy.Pessimistic);
                        await timeoutPolicy.ExecuteAsync(async () => await StartAsync());
                    }
                    else
                    {
                        await StartAsync();
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
                    _status[id].Contexts.TryRemove(flowContext.Id, out _);
                    flowContext.Dispose();
                }

                yield return flowContext.Result;
                if (needBreak || needThrow)
                {
                    break;
                }
            }
        }
        await Dispose(id);
    }
    public void SendEvent(Guid id, string eventName, string? eventData = null)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.SendEvent(eventName, eventData);
        }
    }

    public async Task Dispose(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.Cancel.Cancel();
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

        return default;
    }

    public FlowContext? GetFlowContext(Guid[] ids)
    {
        _status[ids[0]].Contexts.TryGetValue(string.Join(",", ids), out var flowContext);

        return flowContext;
    }

    #endregion

    #region SingleRun


    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];
    public async Task<Guid> Init(ConfigDefinition configDefinition, bool singleRun)
    {
        Guid id = Guid.NewGuid();
        var scope = serviceProvider.CreateScope();

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
        _status[id] = new RunnerStatus(configDefinition, scope)
        {
            Cancel = new(),
            SingleRun = singleRun,
        };
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
                    config.FlowMiddlewares = context.FlowMiddlewares;
                    config.StepGroupMiddlewares = context.StepGroupMiddlewares;
                    config.StepMiddlewares = context.StepMiddlewares;

                    FlowContext subContext = new(embeddedFlow, config, subFlowDefinition.Checkers, [.. context.FlowIds, step.Id], 1, 0, context.Index, context.Logs, context.WaitEvents, context.Data);
                    await SetContextAsync(subContext);
                    _status[id].Contexts.TryAdd(string.Join(",", subContext.FlowIds), subContext);
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

                        var value = await IDataConverterInject.GetValue(step.Inputs.First(v => v.Name == item.Name), serviceProvider, context, item.DefaultValue, default);
                        config.Data.Add(new NameValue(item.Name, value));
                    }
                    config.FlowMiddlewares = context.FlowMiddlewares;
                    config.StepGroupMiddlewares = context.StepGroupMiddlewares;
                    config.StepMiddlewares = context.StepMiddlewares;
                    FlowContext subContext = new(subFlowDefinition, config, subFlowDefinition.Checkers, [.. context.FlowIds, step.Id], 1, 0, context.Index, context.Logs, context.WaitEvents, context.Data);
                    await SetContextAsync(subContext);
                    _status[id].Contexts.TryAdd(string.Join(",", subContext.FlowIds), subContext);
                }
            }
        }

        await SetContextAsync(flowContext);

        _status[id].Contexts.TryAdd(string.Join(",", flowContext.FlowIds), flowContext);

        await Task.CompletedTask;
        return id;
    }

    public async Task ExecuteSingleFlow(Guid id)
    {
        var status = _status[id];

        //foreach (var item in status.Runners)
        //{
        //    if (!status.Contexts.TryGetValue(item.Key, out var flowContext))
        //    {
        //        return;
        //    }
        //    await item.Value.StartSingleFlow(status, flowContext, default);
        //}
    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task RunSingleStep(Guid[] ids, FlowStep flowStep, bool resetContext, ConfigDefinition configDefinition, int index, FlowStep? parentStep = null, CancellationToken cancellationToken = default)
    {
        var id = string.Join(",", ids);
        var status = _status[ids[0]];
        if (!status.Contexts.TryGetValue(id, out var flowContext))
        {
            return;
        }

        if (resetContext)
        {
            if (ids.Length == 1)
            {
                flowContext.ConfigDefinition = configDefinition;
                flowContext.Init();
            }
            else
            {
                if (parentStep is null)
                {
                    return;
                }
                var parentId = string.Join(",", ids.SkipLast(1));
                if (!status.Contexts.TryGetValue(parentId, out var parentContext))
                {
                    return;
                }

                var config = new ConfigDefinition { ConfigName = null, Category = flowStep.Category, Name = flowStep.Name };
                config.FlowMiddlewares = parentContext.FlowMiddlewares;
                config.StepGroupMiddlewares = parentContext.StepGroupMiddlewares;
                config.StepMiddlewares = parentContext.StepMiddlewares;

                foreach (var item in flowContext.FlowDefinition.Data)
                {
                    if (!item.IsInput)
                    {
                        continue;
                    }

                    var value = await IDataConverterInject.GetValue(parentStep.Inputs.First(v => v.Name == item.Name), serviceProvider, flowContext, item.DefaultValue, default);
                    config.Data.Add(new NameValue(item.Name, value));
                }
                flowContext.ConfigDefinition = config;
                flowContext.Init();
            }

        }

        var builder = new MiddlewareBuilder<StepGroupContext>(serviceProvider);

        foreach (var item in flowContext.StepGroupMiddlewares)
        {
            builder.Use(item);
        }

        var application = builder.Build();

        await application.Invoke(new StepGroupContext(flowContext, flowStep, new StepGroupStatus()), status.Cancel.Token);
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }
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


    public void Log(Guid[] ids, StepStatus stepOnceStatus, string log, LogLevel logLevel = LogLevel.Information)
    {
        if (!Contexts.TryGetValue(string.Join(",", ids), out var flowContext))
        {
            return;
        }

        var info = new LogInfo(log, logLevel, DateTime.Now, ids.Last(), stepOnceStatus.Index);
        flowContext.Logs.Add(info);
    }

    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventData"></param>
    /// <returns></returns>
    public void SendEvent(string eventName, string? eventData)
    {
        foreach (var item in Contexts)
        {
            item.Value.EventLogs.Add(new EventLog(DateTime.Now, eventName, eventData));
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