using FlowMaker.Persistence;
using Microsoft.Extensions.Logging;

namespace FlowMaker.Middlewares;

public class LogEventMiddleware(IFlowLogger flowLogWriter) : IEventMiddleware
{
    public async Task OnExecuting(FlowContext flowContext, string eventName, string? eventData, CancellationToken cancellationToken)
    {
        if (flowContext.FlowIds.Length == 1)
        {
            await flowLogWriter.LogEvent(flowContext, eventName, eventData);
        }
    }
}
public class LogFlowMiddleware(IFlowLogger flowLogWriter) : IFlowMiddleware
{
    public async Task OnExecuted(FlowContext flowContext, FlowState runnerState, Exception? exception, CancellationToken cancellationToken)
    {
        if (flowContext.FlowIds.Length == 1)
        {
            await flowLogWriter.LogFlow(flowContext);
        }
    }

    public async Task OnExecuting(FlowContext flowContext, FlowState runnerState, CancellationToken cancellationToken)
    {
        if (flowContext.FlowIds.Length == 1)
        {
            await flowLogWriter.LogFlow(flowContext);
        }
    }
}
public class LogStepMiddleware : IStepMiddleware
{
    public async Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, Exception? exception, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}
public class LogStepOnceMiddleware(IFlowLogger flowLogWriter, ILogger<LogStepOnceMiddleware> logger) : IStepOnceMiddleware
{
    public async Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception? exception, CancellationToken cancellationToken)
    {
        await flowLogWriter.LogStep(flowContext, flowStep, step, stepOnceStatus);
    }

    public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        await flowLogWriter.LogStep(flowContext, flowStep, step, stepOnceStatus);
    }
}
