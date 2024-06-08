using ReactiveUI;
using System.Collections.Concurrent;

namespace FlowMaker.Middlewares;

public class DebugMiddleware : IStepOnceMiddleware, IDisposable
{
    public Dictionary<Guid, List<Guid>> DebugList { get; set; } = [];
    public ConcurrentDictionary<string, TaskCompletionSource> Debugging { get; set; } = [];

    public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception? exception, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
    {
        if (DebugList.TryGetValue(flowContext.FlowIds[0], out var list))
        {
            if (list.Contains(flowStep.Id))
            {
                TaskCompletionSource taskCompletionSource = new();
                Debugging.TryAdd(flowContext.FlowIds[0] + flowStep.Id.ToString(), taskCompletionSource);
                MessageBus.Current.SendMessage(new DebugInfo(flowContext.FlowIds[0], flowStep.Id, true));
                await taskCompletionSource.Task;
            }
        }
    }
    public void AddDebugs(Guid id, List<Guid> stepIds)
    {
        DebugList.Add(id, stepIds);
    }
    public void RemoveDebugs(Guid id)
    {
        DebugList.Remove(id);
    }
    public void AddDebug(Guid id, Guid stepId)
    {
        if (!DebugList.TryGetValue(id, out var list))
        {
            list = [];
            DebugList.Add(id, list);
        }
        list.Add(stepId);
    }
    public void RemoveDebug(Guid id, Guid stepId)
    {
        Continue(id, stepId);
        if (DebugList.TryGetValue(id, out var list))
        {
            list.Remove(stepId);
        }
    }
    public void Continue(Guid id, Guid stepId)
    {
        if (Debugging.TryRemove(id + stepId.ToString(), out var task))
        {
            task.SetResult();
            MessageBus.Current.SendMessage(new DebugInfo(id, stepId, false));
        }
    }

    public void Dispose()
    {
        foreach (var task in Debugging.Values)
        {
            task.SetCanceled();
        }
    }

}
public record DebugInfo(Guid Id, Guid StepId, bool Debugging);
