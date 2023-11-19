using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    private readonly FlowMakerOption _flowMakerOption;
    private Subject<ExcuteStep> ExcuteStepSubject { get; } = new();
    private readonly Subject<Unit> _locker = new();
    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option)
    {
        this._logger = NullLogger<FlowRunner>.Instance;
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
        ExcuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(c =>
        {
            var key = c.Type + c.Type switch
            {
                EventType.Step => c.StepId?.ToString(),
                EventType.Event => c.EventName,
                EventType.Debug => c.EventName,
                EventType.StartFlow => "",
                _ => ""
            };
            if (Context.ExcuteStepIds.TryGetValue(key, out var steps))
            {
                foreach (var item in steps)
                {
                    Context.StepState[item].Waits.Remove(key);
                    if (Context.StepState[item].Waits.Count == 0)
                    {
                        var step = Context.FlowDefinition.Steps.First(c => c.Id == item);

                        _ = Run(step, CancellationTokenSource!.Token);
                    }
                }
            }
            if (Context.StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
            {
                //全部完成
                State = RunnerState.Stop;
            }

            _locker.OnNext(Unit.Default);
        });
    }
    /// <summary>
    /// 全局上下文
    /// </summary>
    public FlowContext Context { get; protected set; } = null!;

    protected async Task RunStep(FlowStep step, StepContext stepContext)
    {
        var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name);
        if (stepDefinition is null)
        {
            throw new Exception();
        }
        var stepObj = _serviceProvider.GetKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);

        //TODO 这里如果找不到步骤就获取所以流程,再执行流程

        if (stepObj is null || CancellationTokenSource is null)
        {
            throw new Exception();
        }

        await stepObj.WrapAsync(Context, stepContext, step, _serviceProvider, CancellationTokenSource.Token);
    }
    protected async Task RunCompensateStep(Guid stepId, StepContext stepContext)
    {
        var step = Context.FlowDefinition.Steps.FirstOrDefault(c => c.Compensate == stepId);
        if (step is null)
        {
            return;
        }

        await RunStep(step, stepContext);
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
    public CancellationTokenSource? CancellationTokenSource { get; protected set; }
    public RunnerState State { get; set; }
    public void Run(FlowDefinition flowInfo)
    {
        if (State != RunnerState.Stop)
        {
            throw new Exception("正在运行中");
        }
        State = RunnerState.Running;
        if (CancellationTokenSource is not null)
        {
            CancellationTokenSource?.Cancel();
        }
        CancellationTokenSource = new CancellationTokenSource();


        Context = new FlowContext(flowInfo);
        Context.InitState();
        ////设置输入
        //foreach (var item in flowInfo.Inputs)
        //{
        //    Context.Data.Add(item.Name, item.Value);
        //}
        ////设置输出
        //foreach (var item in flowInfo.Outputs)
        //{
        //    Context.Data.Add(item.Name, item.Value);
        //}
        //设置全局输入
        //foreach (var item in flowInfo.SetInputs)
        //{
        //    Context.Data.Add(item.Key, item.Value);
        //}
        //执行

        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.StartFlow
        });
    }
    /// <summary>
    /// 停止
    /// </summary>
    public void Stop()
    {
        if (CancellationTokenSource is not null)
        {
            CancellationTokenSource?.Cancel();
        }
        State = RunnerState.Stop;
    }

    private async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.Now;
        try
        {
            StepContext stepContext = new();
            //超时策略
            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(step.TimeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
            //重试策略
            var retryPolicy = Policy.Handle<Exception>().RetryAsync(step.Retry, (exception, retryCount) =>
            {
                _logger.LogWarning($"执行步骤{step.Name}失败，重试次数{retryCount}，异常信息{exception.Message}");
            });
            //回退策略
            var fallbackPolicy = Policy.Handle<Exception>().FallbackAsync(async c =>
            {
                await RunCompensateStep(step.Id, stepContext);
            });

            //组合策略
            var policyWrap = Policy.WrapAsync(timeoutPolicy, retryPolicy, fallbackPolicy);

            for (int i = 0; i < step.Repeat; i++)//重复执行
            {
                stepContext.CurrentIndex = i;
                foreach (var item2 in step.Ifs)
                {
                    var result = await CheckStep(item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        Context.StepState[step.Id].Complete = true;
                        ExcuteStepSubject.OnNext(new ExcuteStep
                        {
                            Type = EventType.Step,
                            StepId = step.Id,
                        });
                        continue;
                    }
                }
                try
                {
                    await policyWrap.ExecuteAsync(async c => await RunStep(step, stepContext), cancellationToken);
                    Context.StepState[step.Id].Results.Add(true);
                }
                catch (Exception e)
                {
                    Context.StepState[step.Id].Results.Add(false);
                    switch (step.ErrorHandling)
                    {
                        case ErrorHandling.Skip:
                            break;
                        case ErrorHandling.Suspend:
                            Context.StepState[step.Id].Suspend = true;
                            return;
                        case ErrorHandling.Terminate:
                            throw new Exception("异常停止", e);
                        default:
                            break;
                    }
                }

            }


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
            Context.StepState[step.Id].ConsumeTime = DateTime.Now - start;
        }
    }


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
