using FlowMaker.Models;
using FlowMaker.Persistence;
using ReactiveUI;
using System.Reactive.Subjects;

namespace FlowMaker.Middlewares;

public class MonitorFlowMiddleware(IFlowProvider flowProvider) : IFlowMiddleware
{
    public int TotalCount { get; set; } = -1;
    public Task OnError(FlowContext flowContext, RunnerState state, Exception exception, CancellationToken cancellationToken)
    {
        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, RunnerState state, CancellationToken cancellationToken)
    {
        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
        return Task.CompletedTask;
    }

    public async Task OnExecuting(FlowContext flowContext, RunnerState state, CancellationToken cancellationToken)
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
        MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state, TotalCount));
    }
}
public class MonitorStepOnceMiddleware : IStepOnceMiddleware
{
    public ReplaySubject<MonitorStepOnceMessage> StepChange { get; set; } = new();
    public Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken)
    {
        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }

    public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }

    public Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        StepChange.OnNext(new MonitorStepOnceMessage(stepOnceStatus, flowContext.FlowIds, flowStep.Id));

        return Task.CompletedTask;
    }
}

public class MonitorMessage(FlowContext context, RunnerState runnerState, int totalCount)
{
    public FlowContext Context { get; set; } = context;
    public RunnerState RunnerState { get; set; } = runnerState;
    public int TotalCount { get; set; } = totalCount;
}
public class MonitorStepOnceMessage(StepOnceStatus stepOnce, Guid[] flowIds, Guid stepId)
{
    public StepOnceStatus StepOnce { get; set; } = stepOnce;
    public Guid[] FlowIds { get; set; } = flowIds;
    public Guid StepId { get; set; } = stepId;
}
