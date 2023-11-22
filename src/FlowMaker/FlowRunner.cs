using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker;

public class FlowRunner
{
    private readonly ILogger<FlowRunner> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowManager _flowManager;
    private readonly FlowMakerOption _flowMakerOption;
    private Subject<ExcuteStep> ExcuteStepSubject { get; } = new();
    private readonly Subject<Unit> _locker = new();
    private CancellationToken _cancellationToken;
    public Guid Id { get; private set; } = Guid.NewGuid();
    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option, FlowManager flowManager)
    {
        this._logger = NullLogger<FlowRunner>.Instance;
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
        this._flowManager = flowManager;
        ExcuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(c =>
        {
            var key = c.Type + c.Type switch
            {
                EventType.Step => c.StepId?.ToString(),
                EventType.Event => c.EventName,
                EventType.EventData => c.EventName,
                EventType.Debug => c.StepId?.ToString() + "Debug",
                EventType.StartFlow => "",
                _ => ""
            };
            if (Context.ExcuteStepIds.TryGetValue(key, out var steps))
            {
                foreach (var item in steps)
                {
                    var step = Context.FlowDefinition.Steps.First(c => c.Id == item);

                    Context.StepState[item].Waits.RemoveAll(c => c == key);
                    if (c.Type == EventType.EventData)//通过事件传递数据
                    {
                        OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.ReceivedEventData));

                        foreach (var input in step.Inputs)
                        {
                            if (input.Mode == InputMode.Event && input.Value == c.EventName)
                            {
                                input.Value = c.EventData;
                            }
                        }
                    }
                    if (c.Type == EventType.Event)
                    {
                        OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.ReceivedEvent));
                    }
                    if (c.Type == EventType.Debug)
                    {
                        OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.ReceivedCancelDebug));
                    }
                    if (Context.StepState[item].Waits.Count == 0)
                    {
                        _ = Run(step, _cancellationToken);
                    }
                }
            }
            if (Context.StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
            {
                //全部完成
                State = RunnerState.Stop;
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
                    TaskCompletionSource.SetResult(results);
                }
            }

            _locker.OnNext(Unit.Default);
        });
    }
    /// <summary>
    /// 全局上下文
    /// </summary>
    protected FlowContext Context { get; set; } = null!;
    protected TaskCompletionSource<List<FlowResult>>? TaskCompletionSource { get; set; }
    protected RunnerState State { get; set; } = RunnerState.Stop;

    public event EventHandler<StepStatusArgs>? OnStepChange;

    protected async Task RunStep(FlowStep step, StepContext stepContext, CancellationToken cancellationToken)
    {
        var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name);
        if (stepDefinition is not null)
        {
            var stepObj = _serviceProvider.GetKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            if (stepObj is null)
            {
                throw new Exception();
            }
            await stepObj.WrapAsync(Context, stepContext, step, _serviceProvider, cancellationToken);
        }
        else
        {
            var subFlowDefinition = await _flowManager.LoadFlowDefinitionAsync(step.Category, step.Name);

            if (subFlowDefinition is null)
            {
                throw new Exception();
            }

            var flowRunner = _serviceProvider.GetRequiredService<FlowRunner>();

            var config = new ConfigDefinition { Category = step.Category, FlowCategory = step.Category, FlowName = step.Name, Name = "" };
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = await IDataConverterInject.GetValue(step.Inputs.First(v => v.Name == item.Name), _serviceProvider, Context, item.DefaultValue, cancellationToken);
                config.Data.Add(new NameValue(item.Name, value));
            }
            var results = await flowRunner.Start(subFlowDefinition, config, cancellationToken);

            foreach (var item in results)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
        }
    }
    protected async Task<bool> CheckStep(Guid convertId, CancellationToken cancellationToken)
    {
        var converter = Context.FlowDefinition.Checkers.FirstOrDefault(c => c.Id == convertId);
        if (converter is null)
        {
            throw new Exception();
        }
        return await IDataConverter<bool>.GetValue(converter, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
    }
    public async Task<List<FlowResult>> Start(FlowDefinition flowInfo, ConfigDefinition config, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        if (State != RunnerState.Stop)
        {
            throw new Exception("正在运行中");
        }
        State = RunnerState.Running;

        TaskCompletionSource = new TaskCompletionSource<List<FlowResult>>();

        Context = new FlowContext(flowInfo);
        Context.InitState();

        foreach (var item in flowInfo.Data)//写入globe data
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

        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.StartFlow,
        });

        return await TaskCompletionSource.Task;
    }

    public void SendEventData(string eventName, string eventData)
    {
        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.EventData,
            EventData = eventData,
            EventName = eventName
        });
    }
    public void SendEvent(string eventName)
    {
        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.Event,
            EventName = eventName
        });
    }
    public void SendDebug(Guid stepId, bool debug)
    {
        if (debug)
        {
            var key = stepId.ToString() + "Debug";
            Context.ExcuteStepIds[key].Add(stepId);
            Context.StepState[stepId].Waits.Add(key);
        }
        else
        {
            ExcuteStepSubject.OnNext(new ExcuteStep
            {
                Type = EventType.Debug,
                StepId = stepId
            });
        }
    }
    protected async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.Now;
        try
        {
            StepContext stepContext = new();

            List<IAsyncPolicy> policies = [];
            //重试策略
            if (step.Retry > 0)
            {
                var retryPolicy = Policy.Handle<Exception>().RetryAsync(step.Retry, (exception, retryCount) =>
                {
                    stepContext.ErrorIndex++;
                    _logger.LogWarning($"执行步骤{step.Name}失败，重试次数{retryCount}，异常信息{exception.Message}");
                });
                policies.Add(retryPolicy);
            }


            //回退策略
            var campensateStep = Context.FlowDefinition.Steps.FirstOrDefault(c => c.Compensate == step.Id);
            if (campensateStep is not null)
            {
                var fallbackPolicy = Policy.Handle<Exception>().FallbackAsync(async c =>
                {
                    await RunStep(campensateStep, stepContext, cancellationToken);
                });
                policies.Add(fallbackPolicy);
            }

            //超时策略
            if (step.TimeOut > 0)
            {
                var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(step.TimeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
                policies.Add(timeoutPolicy);
            }

            for (int i = 0; i < step.Repeat; i++)//重复执行
            {
                stepContext.CurrentIndex = i;
                stepContext.ErrorIndex = 0;
                foreach (var item2 in step.Ifs)
                {
                    var result = await CheckStep(item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.Skip));

                        //跳过
                        Context.StepState[step.Id].Complete = true;
                        ExcuteStepSubject.OnNext(new ExcuteStep
                        {
                            Type = EventType.Step,
                            StepId = step.Id,
                        });
                        return;
                    }
                }
                try
                {
                    OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.Start));
                    //组合策略
                    var policyWrap = Policy.WrapAsync(policies.ToArray());
                    await policyWrap.ExecuteAsync(async c => await RunStep(step, stepContext, c), cancellationToken);
                    Context.StepState[step.Id].Results.Add(true);
                    OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.Complete));
                }
                catch (Exception e)
                {
                    OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.Error));

                    Context.StepState[step.Id].Results.Add(false);
                    switch (step.ErrorHandling)
                    {
                        case ErrorHandling.Skip:
                            break;
                        case ErrorHandling.Suspend:
                            Context.StepState[step.Id].Suspend = true;
                            return;
                        case ErrorHandling.Terminate:
                            TaskCompletionSource?.SetException(e);
                            return;
                        default:
                            break;
                    }
                }
            }

            OnStepChange?.Invoke(this, new StepStatusArgs(step, StepStatus.AllComplete));

            Context.StepState[step.Id].Complete = true;
            Context.StepState[step.Id].ConsumeTime = DateTime.Now - start;

            //执行下一步
            ExcuteStepSubject.OnNext(new ExcuteStep
            {
                Type = EventType.Step,
                StepId = step.Id,
            });
        }

        catch (Exception e)
        {
            TaskCompletionSource?.SetException(e);
        }
    }


}

public class StepStatusArgs(FlowStep step, StepStatus status)
{
    public FlowStep Step { get; set; } = step;

    public StepStatus Status { get; set; } = status;
}

public enum StepStatus
{
    Start,
    Complete,
    AllComplete,
    ReceivedEvent,
    ReceivedEventData,
    ReceivedCancelDebug,
    Error,
    Skip
}

/// <summary>
/// 事件
/// </summary>
public class ExcuteStep
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
    Step,
    Event,
    EventData,
    Debug,
    StartFlow,
}
/// <summary>
/// 步骤运行状态
/// </summary>
public enum RunnerState
{
    Running,
    Suspend,
    Stop
}
