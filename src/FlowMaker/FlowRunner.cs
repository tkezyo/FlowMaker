using FlowMaker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;

namespace FlowMaker;

public class FlowRunner
{
    private readonly ILogger<FlowRunner> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowMakerOption _flowMakerOption;

    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option)
    {
        this._logger = NullLogger<FlowRunner>.Instance;
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
    }
    /// <summary>
    /// 全局上下文
    /// </summary>
    public FlowContext Context { get; protected set; } = null!;

    protected async Task RunStep(StepContext step)
    {
        var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name);
        var stepObj = _serviceProvider.GetService(stepDefinition.Type);

        if (stepObj is not IStep stepService || CancellationTokenSource is null)
        {
            throw new Exception();
        }

        await stepService.WrapAsync(Context, step, _serviceProvider, CancellationTokenSource.Token);
    }
    protected async Task RunCompensateStep(Guid stepId)
    {
        var step = Context.FlowDefinition.CompensateSteps.FirstOrDefault(c => c.Id == stepId);
        if (step is null)
        {
            throw new Exception();
        }

        await RunStep();
    }

    protected async Task<bool> CheckStep(Guid convertId, CancellationToken cancellationToken)
    {
        var converter = Context.FlowDefinition.ExcuteCheckers.FirstOrDefault(c => c.Id == convertId);
        if (converter is null)
        {
            throw new Exception();
        }
        return await IFlowValueConverter<bool>.GetValue(converter, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
    }
    public CancellationTokenSource? CancellationTokenSource { get; protected set; }
    public RunnerState State { get; set; }
    public void Run(FlowDefinition flowInfo)
    {
        Context = new FlowContext(flowInfo);
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
        //初始化
        foreach (var item in flowInfo.Steps)
        {
            Context.StepState.Add(item.Id, new StepResult());
        }
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
        RunNext(null, CancellationTokenSource.Token);
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
    /// <summary>
    /// 恢复
    /// </summary>
    public void Resume()
    {

    }
    /// <summary>
    /// 暂停
    /// </summary>
    /// <param name="stepId"></param>
    public void Suspend(Guid stepId)
    {
        if (Context.StepState[stepId].Suspend)
        {
            return;
        }
        Context.StepState[stepId].Suspend = true;
        Context.SuspendSteps.Add(stepId);
    }

    /// <summary>
    /// 检查是否全部完成
    /// </summary>
    public void CheckComplete()
    {
        if (Context.StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
        {
            //全部完成
            State = RunnerState.Stop;
        }
    }

    public void RunNext(Guid? preStepId, CancellationToken cancellationToken)
    {
        //获取所有依赖于preActionId的任务
        var list = Context.FlowDefinition.Steps.Where(c =>
        {
            if (preStepId.HasValue)
            {
                return c.PreSteps.Contains(preStepId.Value) && !Context.StepState[c.Id].Started;
            }
            else
            {
                return !c.PreSteps.Any() && !Context.StepState[c.Id].Started;
            }
        });

        foreach (var item in list)
        {
            bool canNext = true;
            //确定是否满足条件
            foreach (var preAction in item.PreSteps)
            {
                if (!Context.StepState[preAction].Complete)
                {
                    canNext = false;
                    break;
                }
            }
            if (!canNext)
            {
                continue;
            }
            //确定是否被取消
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (Context.StepState[item.Id].Started)//已经开始执行了，不再执行
            {
                return;
            }
            if (Context.StepState[item.Id].Suspend)//已经暂停了，不再执行
            {
                return;
            }
            Context.StepState[item.Id].Started = true;
            //执行
            _ = Run(item, cancellationToken);
        }
    }

    private async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.Now;
        try
        {
            //超时策略
            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(step.TimeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
            //重试策略
            var retryPolicy = Policy.Handle<Exception>().RetryAsync(step.Retry, (exception, retryCount) =>
            {
                _logger.LogWarning($"执行步骤{step.Name}失败，重试次数{retryCount}，异常信息{exception.Message}");
            });
            //回退策略
            var fallbackPolicy = Policy.Handle<Exception>().FallbackAsync((Func<CancellationToken, Task>)(async c =>
            {
                if (!step.Catch.HasValue)
                {
                    return;
                }

                await RunCompensateStep(step.Catch.Value);
            }));

            //组合策略
            var policyWrap = Policy.WrapAsync(timeoutPolicy, retryPolicy, fallbackPolicy);

            for (int i = 0; i < step.Repeat; i++)//重复执行
            {
                foreach (var item2 in step.If)
                {
                    var result = await CheckStep(item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        continue;
                    }
                }
                try
                {
                    await policyWrap.ExecuteAsync(async c => await RunStep(step), cancellationToken);
                    Context.StepState[step.Id].CompleteTimes++;
                }
                catch (Exception e)
                {
                    Context.StepState[step.Id].ErrorTimes++;
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

            CheckComplete();
            //执行下一步
            RunNext(step.Id, cancellationToken);
        }

        catch (Exception e)
        {
            Context.StepState[step.Id].ConsumeTime = DateTime.Now - start;

        }
    }
}

public enum RunnerState
{
    Running,
    Suspend,
    Stop
}
