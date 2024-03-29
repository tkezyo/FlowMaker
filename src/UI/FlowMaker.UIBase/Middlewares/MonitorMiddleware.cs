﻿using FlowMaker.Models;
using FlowMaker.Persistence;
using ReactiveUI;
using System.Reactive.Subjects;

namespace FlowMaker.Middlewares;

public class MonitorMiddleware(IFlowProvider flowProvider) : IFlowMiddleware, IStepOnceMiddleware
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

    public Task OnError(FlowContext flowContext, FlowState state, Exception exception, CancellationToken cancellationToken)
    {
        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
        return Task.CompletedTask;
    }

    public Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken)
    {
        PercentChange.OnNext(Percent);
        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, FlowState state, CancellationToken cancellationToken)
    {

        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        if (stepOnceStatus.State == StepOnceState.Complete && stepOnceStatus.EndTime.HasValue)
        {
            CompleteCount += 0.5;
            Percent = (double)CompleteCount / TotalCount * 100;
        }
        PercentChange.OnNext(Percent);
        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }

    public async Task OnExecuting(FlowContext flowContext, FlowState state, CancellationToken cancellationToken)
    {
        if (TotalCount == -1)
        {
            var definition = await flowProvider.LoadFlowDefinitionAsync(flowContext.FlowDefinition.Category, flowContext.FlowDefinition.Name);
            if (definition is null)
            {
                return;
            }

            TotalCount = 0;

            async Task SetFlowStepAsync(FlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (!item.IsSubFlow)
                    {
                        TotalCount++;
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
        }

        CompleteCount = 0;
        Percent = 0;
        PercentChange.OnNext(Percent);
        StepChange.Dispose();
        StepChange = new ReplaySubject<MonitorStepOnceMessage>(TotalCount);
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
        PercentChange.OnNext(Percent);

        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }
}

public class MonitorMessage(FlowContext context, FlowState runnerState, int totalCount)
{
    public FlowContext Context { get; set; } = context;
    public FlowState RunnerState { get; set; } = runnerState;
    public int TotalCount { get; set; } = totalCount;
}
public class MonitorStepOnceMessage(StepOnceStatus stepOnce, Guid[] flowIds, Guid stepId)
{
    public StepOnceStatus StepOnce { get; set; } = stepOnce;
    public Guid[] FlowIds { get; set; } = flowIds;
    public Guid StepId { get; set; } = stepId;
}
