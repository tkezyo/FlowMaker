using FlowMaker;
using FlowMaker.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Test1
{
    public class StepOnceMiddleware : IStepOnceMiddleware
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
