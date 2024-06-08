using DynamicData;
using DynamicData.Binding;
using FlowMaker.Persistence;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker.Middlewares;

public class MonitorMiddleware(IFlowProvider flowProvider) : IFlowMiddleware, IStepMiddleware, IStepOnceMiddleware, IDisposable
{
    public int TotalCount { get; set; } = -1;
    public double CompleteCount { get; set; }
    public double Percent { get; set; }
    /// <summary>
    /// 步骤变化
    /// </summary>
    public ReplaySubject<MonitorStepOnceMessage> StepChange { get; set; } = new();
    /// <summary>
    /// 百分比变化
    /// </summary>
    public ReplaySubject<double> PercentChange { get; set; } = new(1);

    public Task OnExecuted(FlowContext flowContext, FlowState state, Exception? exception, CancellationToken cancellationToken)
    {
        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));

        //清除StepChange中的数据
        StepChange.Dispose();
        PercentChange.Dispose();
        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception? exception, CancellationToken cancellationToken)
    {

        if (stepOnceStatus.EndTime.HasValue)
        {
            CompleteCount += 0.5;
            Percent = (double)CompleteCount / TotalCount * 100;
        }
        if (!PercentChange.IsDisposed)
        {
            PercentChange.OnNext(Percent);
        }
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(flowContext, step, stepOnceStatus, flowContext.FlowIds, flowStep));
        }

        return Task.CompletedTask;
    }

    public async Task OnExecuting(FlowContext flowContext, FlowState state, CancellationToken cancellationToken)
    {
        if (flowContext.FlowIds.Length == 1)
        {
            var definition = await flowProvider.LoadFlowDefinitionAsync(flowContext.ConfigDefinition.Category, flowContext.ConfigDefinition.Name);
            if (definition is null)
            {
                return;
            }

            TotalCount = 0;

            async Task SetFlowStepAsync(IFlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (item.Type == StepType.Normal)
                    {
                        TotalCount++;
                    }
                    else if (item.Type == StepType.Embedded && flowDefinition is FlowDefinition fde)
                    {
                        var stepDefinition = fde.EmbeddedFlows.First(c => c.StepId == item.Id);

                        await SetFlowStepAsync(stepDefinition);
                    }
                    else
                    {
                        var stepDefinition = await flowProvider.GetStepDefinitionAsync(item.Category, item.Name);
                        if (stepDefinition is FlowDefinition fd)
                        {
                            await SetFlowStepAsync(fd);
                        }
                    }
                }
            }

            await SetFlowStepAsync(definition);

            CompleteCount = 0;
            Percent = 0;
            StepChange.Dispose();
            StepChange = new ReplaySubject<MonitorStepOnceMessage>(TotalCount);
            PercentChange.Dispose();
            PercentChange = new ReplaySubject<double>(1);

        }
        if (!PercentChange.IsDisposed)
        {
            PercentChange.OnNext(Percent);
        }

        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
    }

    public Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        if (stepOnceStatus.State == StepOnceState.Start && stepOnceStatus.StartTime.HasValue)
        {
            CompleteCount += 0.5;
            Percent = (double)CompleteCount / TotalCount * 100;

        }

        if (stepOnceStatus.State == StepOnceState.Skip)
        {
            CompleteCount += 1;
            Percent = (double)CompleteCount / TotalCount * 100;
        }

        if (!PercentChange.IsDisposed)
        {
            PercentChange.OnNext(Percent);
        }
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(flowContext, step, stepOnceStatus, flowContext.FlowIds, flowStep));
        }
        return Task.CompletedTask;
    }

    public Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken)
    {
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(flowContext, step, null, flowContext.FlowIds, flowStep));
        }
        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, Exception? exception, CancellationToken cancellationToken)
    {
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(flowContext, step, null, flowContext.FlowIds, flowStep));
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StepChange.Dispose();
        PercentChange.Dispose();
    }
}

public class MonitorMessage(FlowContext context, FlowState runnerState, int totalCount)
{
    public FlowContext Context { get; set; } = context;
    public FlowState RunnerState { get; set; } = runnerState;
    public int TotalCount { get; set; } = totalCount;
}
public class MonitorStepOnceMessage(FlowContext flowContext, StepStatus stepStatus, StepOnceStatus? stepOnce, Guid[] flowIds, FlowStep step)
{
    public FlowContext FlowContext { get; set; } = flowContext;
    public StepStatus StepStatus { get; } = stepStatus;
    public StepOnceStatus? StepOnce { get; set; } = stepOnce;
    public Guid[] FlowIds { get; set; } = flowIds;
    public FlowStep Step { get; set; } = step;
}
