using FlowMaker;
using FlowMaker.Models;
using ReactiveUI;
using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace FlowMaker.Middlewares
{
    public class MonitorFlowMiddleware : IFlowMiddleware
    {
        public Task OnError(FlowContext flowContext, RunnerState state, Exception exception, CancellationToken cancellationToken)
        {
            MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state));
            return Task.CompletedTask;
        }

        public Task OnExecuted(FlowContext flowContext, RunnerState state, CancellationToken cancellationToken)
        {
            MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state));
            return Task.CompletedTask;
        }

        public Task OnExecuting(FlowContext flowContext, RunnerState state, CancellationToken cancellationToken)
        {
            MessageBus.Current.SendMessage(new MonitorMessage(flowContext, state));
            return Task.CompletedTask;
        }
    }
    public class MonitorMiddleware : IStepOnceMiddleware
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

    public class MonitorMessage(FlowContext context, RunnerState runnerState)
    {
        public FlowContext Context { get; set; } = context;

        public RunnerState RunnerState { get; set; } = runnerState;
    }
    public class MonitorStepOnceMessage(StepOnceStatus stepOnce, Guid[] flowIds, Guid stepId)
    {
        public StepOnceStatus StepOnce { get; set; } = stepOnce;
        public Guid[] FlowIds { get; set; } = flowIds;
        public Guid StepId { get; set; } = stepId;
    }
}
