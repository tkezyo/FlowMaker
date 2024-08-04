using ReactiveUI;
using System.Collections.Concurrent;

namespace FlowMaker.Middlewares;

public class DebugMiddleware : IMiddleware<StepContext>, IDisposable
{
    public const string Name = "debug";
    public Dictionary<Guid, List<Guid>> DebugList { get; set; } = [];
    public ConcurrentDictionary<string, TaskCompletionSource> Debugging { get; set; } = [];

    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        if (DebugList.TryGetValue(context.FlowContext.FlowIds[0], out var list))
        {
            if (list.Contains(context.Step.Id))
            {
                TaskCompletionSource taskCompletionSource = new();
                Debugging.TryAdd(context.FlowContext.FlowIds[0] + context.Step.Id.ToString(), taskCompletionSource);
                MessageBus.Current.SendMessage(new DebugInfo(context.FlowContext.FlowIds[0], context.Step.Id, true));
                await taskCompletionSource.Task;
            }
        }

        await next(context, cancellationToken);
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
