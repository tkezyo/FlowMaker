using FlowMaker.Models;
using FlowMaker.Persistence;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker.Middlewares
{
    public class LogEventMiddleware(IFlowLogWriter flowLogWriter) : IEventMiddleware
    {
        public async Task OnExecuting(FlowContext flowContext, string eventName, string? eventData, CancellationToken cancellationToken)
        {
            if (flowContext.FlowIds.Length == 1)
            {
                await flowLogWriter.LogEvent(flowContext, eventName, eventData);
            }
        }
    }
    public class LogFlowMiddleware(IFlowLogWriter flowLogWriter) : IFlowMiddleware
    {
        public async Task OnError(FlowContext flowContext, RunnerState runnerState, Exception exception, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task OnExecuted(FlowContext flowContext, RunnerState runnerState, CancellationToken cancellationToken)
        {
            if (flowContext.FlowIds.Length == 1)
            {
                await flowLogWriter.LogFlow(flowContext);
            }
        }

        public async Task OnExecuting(FlowContext flowContext, RunnerState runnerState, CancellationToken cancellationToken)
        {
            if (flowContext.FlowIds.Length == 1)
            {
                await flowLogWriter.LogFlow(flowContext);
            }
        }
    }
    public class LogStepMiddleware : IStepMiddleware
    {
        public async Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, Exception exception, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

        }

        public async Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
    public class LogStepOnceMiddleware(IFlowLogWriter flowLogWriter) : IStepOnceMiddleware
    {
        public async Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            await flowLogWriter.LogStep(flowContext, flowStep, step, stepOnceStatus);
        }

        public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            await flowLogWriter.LogStep(flowContext, flowStep, step, stepOnceStatus);
        }
    }
}
