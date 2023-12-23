using FlowMaker;
using FlowMaker.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Test1
{
    //TODO 需要改成单例
    public class DebugMiddleware : IStepOnceMiddleware, IDisposable
    {
        public List<Guid> DebugList { get; set; } = [];
        public ConcurrentDictionary<Guid, TaskCompletionSource> Debugging { get; set; } = [];
        public Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken)
        {
            if (DebugList.Contains(flowStep.Id))
            {
                TaskCompletionSource taskCompletionSource = new();
                Debugging.TryAdd(flowStep.Id, taskCompletionSource);
                await taskCompletionSource.Task;
            }
        }

        public void AddDebug(Guid id)
        {
            if (!DebugList.Contains(id))
            {
                DebugList.Add(id);
            }
        }
        public void RemoveDebug(Guid id)
        {
            Continue(id);
            if (DebugList.Contains(id))
            {
                DebugList.Remove(id);
            }
        }
        public void Continue(Guid id)
        {
            if (Debugging.TryRemove(id, out var task))
            {
                task.SetResult();
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
}
