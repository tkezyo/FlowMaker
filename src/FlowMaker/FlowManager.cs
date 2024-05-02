using DynamicData;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Splat;
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly FlowMakerOption _flowMakerOption;


    public FlowManager(IServiceProvider serviceProvider, IOptions<FlowMakerOption> options, IFlowProvider flowProvider, ILogger<FlowManager> logger, ILoggerFactory loggerFactory)
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
        this._loggerFactory = loggerFactory;
    }

    #region Run
    class RunnerStatus(ConfigDefinition config, FlowRunner flowRunner, IServiceScope serviceScope)
    {
        public ConfigDefinition Config { get; set; } = config;

        public IServiceScope ServiceScope { get; set; } = serviceScope;
        public FlowRunner FlowRunner { get; set; } = flowRunner;

        public CancellationTokenSource Cancel { get; set; } = new();
    }
    public IEnumerable<FlowRunner> RunningFlows => _status.Values.Select(c => c.FlowRunner);
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
        var runner = scope.ServiceProvider.GetRequiredService<FlowRunner>();
        _status[runner.Id] = new RunnerStatus(configDefinition, runner, scope)
        {
            Cancel = new CancellationTokenSource()
        };
        await Task.CompletedTask;
        return runner.Id;
    }

    public async IAsyncEnumerable<FlowResult> Run(Guid id)
    {
        var status = _status[id];
        var runner = _status[id].FlowRunner;
        var testName = DateTime.Now.ToString("yyyyMMdd") + runner.Id;

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
            if (_status[id].Cancel.IsCancellationRequested)
            {
                break;
            }
            if (needThrow)
            {
                break;
            }

            var errorTimes = 0;
            while (!_status[id].Cancel.IsCancellationRequested)
            {
                FlowResult? flowResult = null;
                bool needBreak = false;
                try
                {
                    if (status.Config.Timeout > 0)
                    {
                        var timeoutPolicy = Policy.TimeoutAsync<FlowResult>(TimeSpan.FromSeconds(status.Config.Timeout), Polly.Timeout.TimeoutStrategy.Pessimistic);
                        flowResult = await timeoutPolicy.ExecuteAsync(async () => await runner.Start(flow, flow.Checkers, status.Config, [], i, errorTimes, _status[id].Cancel.Token));
                    }
                    else
                    {
                        flowResult = await runner.Start(flow, flow.Checkers, status.Config, [], i, errorTimes, _status[id].Cancel.Token);
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
        await Dispose(runner.Id);

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
            await status.FlowRunner.StopAsync();
            status.Cancel.Cancel();
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
