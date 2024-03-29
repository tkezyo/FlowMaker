﻿using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml.Linq;

namespace FlowMaker;

public class FlowManager
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFlowProvider _flowProvider;
    private readonly ILogger<FlowManager> _logger;
    private readonly FlowMakerOption _flowMakerOption;


    public FlowManager(IServiceProvider serviceProvider, IOptions<FlowMakerOption> options, IFlowProvider flowProvider, ILogger<FlowManager> logger)
    {
        _jsonSerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
            IncludeFields = false
        };
        _flowMakerOption = options.Value;
        this._serviceProvider = serviceProvider;
        this._flowProvider = flowProvider;
        this._logger = logger;
    }

    #region Run
    class RunnerStatus(FlowRunner flowRunner, IServiceScope serviceScope)
    {
        public string? ConfigName { get; set; }
        /// <summary>
        /// 流程的类别
        /// </summary>
        public required string Category { get; set; }
        /// <summary>
        /// 流程的名称
        /// </summary>
        public required string Name { get; set; }

        public IServiceScope ServiceScope { get; set; } = serviceScope;
        public FlowRunner FlowRunner { get; set; } = flowRunner;

        public CancellationTokenSource Cancel { get; set; } = new();
    }
    public IEnumerable<FlowRunner> RunningFlows => _status.Values.Select(c => c.FlowRunner);
    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];

    public async Task<List<FlowResult>> Run(string configName, string flowCategory, string flowName)
    {
        var config = await _flowProvider.LoadConfigDefinitionAsync(flowCategory, flowName, configName);
        if (config is null)
        {
            throw new InvalidOperationException("未找到配置");
        }
        return await Run(config);
    }

    public async Task<List<FlowResult>> Run(ConfigDefinition configDefinition, Action<Guid>? Init = null)
    {
        var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<FlowRunner>();
        _status[runner.Id] = new RunnerStatus(runner, scope)
        {
            ConfigName = configDefinition.ConfigName,
            Category = configDefinition.Category,
            Name = configDefinition.Name
        };
        _status[runner.Id].Cancel = new CancellationTokenSource();
        Init?.Invoke(runner.Id);

        var flow = await _flowProvider.LoadFlowDefinitionAsync(configDefinition.Category, configDefinition.Name);
        if (flow is null)
        {
            throw new InvalidOperationException("未找到流程");
        }
        List<FlowResult> result = [];
        for (int i = 0; i < configDefinition.Repeat; i++)
        {
            var errorTimes = 0;
            while (!_status[runner.Id].Cancel.IsCancellationRequested)
            {
                try
                {
                    if (configDefinition.Timeout > 0)
                    {
                        var timeoutPolicy = Policy.TimeoutAsync<FlowResult>(TimeSpan.FromSeconds(configDefinition.Timeout), Polly.Timeout.TimeoutStrategy.Pessimistic);
                        result.Add(await timeoutPolicy.ExecuteAsync(async () => await runner.Start(flow, configDefinition, [], i, errorTimes, _status[runner.Id].Cancel.Token)));
                    }
                    else
                    {
                        result.Add(await runner.Start(flow, configDefinition, [], i, errorTimes, _status[runner.Id].Cancel.Token));
                    }
                    break;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("流程被取消:{Id}", runner.Id);
                }
                catch (Exception e)
                {
                    result.Add(new FlowResult
                    {
                        ErrorIndex = errorTimes,
                        Exception = e,
                        CurrentIndex = i,
                        Success = false,
                    });
                    if (errorTimes >= configDefinition.Retry)
                    {
                        if (configDefinition.ErrorHandling == ErrorHandling.Skip)
                        {
                            break;
                        }
                        switch (configDefinition.ErrorHandling)
                        {
                            case ErrorHandling.Terminate:
                                throw new Exception("流程执行失败", e);
                            default:
                                break;
                        }
                    }
                }
            }
        }
        await Dispose(runner.Id);
        return result;

    }
    public async Task SendEvent(Guid id, string eventName, string? eventData = null)
    {
        if (_status.TryGetValue(id, out var status))
        {
            await status.FlowRunner.SendEventAsync(eventName, eventData);
        }
    }
    public async Task Stop(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.Cancel.Cancel();
            await status.FlowRunner.StopAsync();
        }
    }

    public async Task Dispose(Guid id)
    {
        if (_status.TryGetValue(id, out var status))
        {
            status.ServiceScope.Dispose();
            while (status.FlowRunner.State == FlowState.Running)
            {
                await Task.Delay(300);
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
