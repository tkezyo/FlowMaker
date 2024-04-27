using Microsoft.Extensions.Logging;

namespace FlowMaker.Persistence;



public interface IFlowLogger
{
    Task<FlowLog[]> GetFlowLog(Guid id);
    Task LogFlow(FlowContext flowContext, Exception? exception = null);
    Task LogStep(FlowContext flowContext, FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus, Exception? exception = null);
    Task LogEvent(FlowContext flowContext, string eventName, string? eventData);
    Task LogMiddleware(Guid id, List<string> middlewares);
}