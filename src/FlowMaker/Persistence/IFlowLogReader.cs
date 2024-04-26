namespace FlowMaker.Persistence;

public interface IFlowLogReader
{
    Task<FlowLog[]> GetFlowLog(Guid id);
}


public interface IFlowLogWriter
{
    Task LogFlow(FlowContext flowContext, Exception? exception = null);
    Task LogStep(FlowContext flowContext, FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus, Exception? exception = null);
    Task LogEvent(FlowContext flowContext, string eventName, string? eventData);
    Task LogMiddleware(Guid id, List<string> middlewares);
}