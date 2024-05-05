﻿using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Splat;
using System.Collections.Concurrent;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;

namespace FlowMaker;

/// <summary>
/// 管理所有的流程，包括流程的初始化，运行，停止，事件发送等
/// </summary>
/// <param name="serviceProvider"></param>
/// <param name="flowProvider"></param>
/// <param name="logger"></param>
public class FlowManager(IServiceProvider serviceProvider, IFlowProvider flowProvider, ILogger<FlowManager> logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IFlowProvider _flowProvider = flowProvider;
    private readonly ILogger<FlowManager> _logger = logger;

    #region Run
    class RunnerStatus(ConfigDefinition config, IServiceScope serviceScope)
    {
        public ConfigDefinition Config { get; set; } = config;

        public IServiceScope ServiceScope { get; set; } = serviceScope;
        public FlowRunner? FlowRunner { get; set; }
        public SourceCache<FlowContext, string> FlowContexts { get; set; } = new(c => $"{c.CurrentIndex}.{c.ErrorIndex}");

        public CancellationTokenSource Cancel { get; set; } = new();
    }

    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];

    public async IAsyncEnumerable<FlowResult> Run(string configName, string flowCategory, string flowName)
    {
        var config = await _flowProvider.LoadConfigDefinitionAsync(flowCategory, flowName, configName);
        if (config is null)
        {
            throw new InvalidOperationException("未找到配置");
        }
        var id = await Init(config);
        await foreach (var item in Run(id))
        {
            yield return item;
        }
    }

    public async Task<Guid> Init(ConfigDefinition configDefinition)
    {
        var scope = _serviceProvider.CreateScope();
        Guid id = Guid.NewGuid();
        _status[id] = new RunnerStatus(configDefinition, scope)
        {
            Cancel = new CancellationTokenSource()
        };
        await Task.CompletedTask;
        return id;
    }

    public async IAsyncEnumerable<FlowResult> Run(Guid id)
    {
        var status = _status[id];
        if (status.FlowRunner is not null)
        {
            status.FlowRunner.Dispose();
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

        for (int i = 1; i <= status.Config.Repeat; i++)
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
                try
                {
                    var runner = status.ServiceScope.ServiceProvider.GetRequiredService<FlowRunner>();

                    status.FlowRunner = runner;
                    FlowContext flowContext = new(status.Config, [id], i, errorTimes);
                    status.FlowContexts.AddOrUpdate(flowContext);
                    if (status.Config.Timeout > 0)
                    {
                        var timeoutPolicy = Policy.TimeoutAsync<FlowResult>(TimeSpan.FromSeconds(status.Config.Timeout), Polly.Timeout.TimeoutStrategy.Pessimistic);
                        flowResult = await timeoutPolicy.ExecuteAsync(async () => await runner.Start(flow, flow.Checkers, flowContext, status.Cancel.Token));
                    }
                    else
                    {
                        flowResult = await runner.Start(flow, flow.Checkers, flowContext, _status[id].Cancel.Token);
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
            status.ServiceScope.Dispose();
            if (status.FlowRunner is not null)
            {
                while (status.FlowRunner.State == FlowState.Running)
                {
                    await Task.Delay(300);
                }
            }
            _status.Remove(id, out _);
        }
    }

    public T? GetRunnerService<T>(Guid id, string? key = null)
    {
        if (_status.TryGetValue(id, out var status))
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
    #endregion
}
