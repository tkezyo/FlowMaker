using FlowMaker.Models;

namespace FlowMaker.Persistence;

public interface IFlowLogReader
{
    Task<FlowLog[]> GetFlowLog(Guid id);
}


public interface IFlowLogWriter
{
    Task LogFlow(FlowContext flowContext);
    Task LogStep(FlowContext flowContext, FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus);
    Task LogEvent(FlowContext flowContext, string eventName, string? eventData);
    Task LogMiddleware(Guid id, List<string> middlewares);
}