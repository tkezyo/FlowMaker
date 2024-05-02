using DynamicData;
using System.Reactive.Linq;
using System.Collections.Concurrent;

namespace FlowMaker.Persistence
{
    public class MemoryFlowLogProvider : IFlowLogger
    {
        public ConcurrentDictionary<Guid, SourceCache<FlowLog, string>> Logs { get; set; } = [];
        public async Task<FlowLog[]> GetFlowLog(Guid id)
        {
            Logs.TryGetValue(id, out var log);
            await Task.CompletedTask;
            return log?.Items.ToArray() ?? [];
        }
        public async Task Move(Guid id)
        {
            await Task.CompletedTask;
            Logs.TryRemove(id, out _);
        }
        public async Task LogFlow(FlowContext flowContext, Exception? exception = null)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var logs))
            {
                logs = new SourceCache<FlowLog, string>(c => c.CurrentIndex + "," + c.ErrorIndex);
                Logs.TryAdd(flowContext.FlowIds[0], logs);
            }

            var last = logs.Lookup(flowContext.CurrentIndex + "," + flowContext.ErrorIndex);

            if (!last.HasValue)
            {
                var log = new FlowLog
                {
                    Id = flowContext.FlowIds[0],
                    Category = flowContext.FlowDefinition.Category,
                    Name = flowContext.FlowDefinition.Name,
                    StartTime = DateTime.Now,
                    CurrentIndex = flowContext.CurrentIndex,
                    ErrorIndex = flowContext.ErrorIndex,
                    Middlewares = flowContext.Middlewares
                };
                logs.AddOrUpdate(log);
            }
            else
            {
                last.Value.EndTime = DateTime.Now;
                logs.AddOrUpdate(last.Value);
            }


            await Task.CompletedTask;
        }


        public async Task LogEvent(FlowContext flowContext, string eventName, string? eventData)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var logs))
            {
                return;
            }
            var log = logs.Items.Last();

            log.Events.Add(new EventLog
            {
                EventName = eventName,
                EventData = eventData,
                Time = DateTime.Now
            });

            await Task.CompletedTask;
        }

        public async Task LogStep(FlowContext flowContext, FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus, Exception? exception = null)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var logs))
            {
                return;
            }
            var log = logs.Items.Last();
            var stepLogOp = log.StepLogs.Lookup(flowStep.Id);
            if (!stepLogOp.HasValue)
            {
                var stepLog = new StepLog
                {
                    FlowIds = flowContext.FlowIds,
                    StepName = flowStep.DisplayName,
                    StepId = flowStep.Id,
                    StartTime = DateTime.Now,
                };
                stepLog.StepOnceLogs.AddOrUpdate(stepOnceStatus);
                log.StepLogs.AddOrUpdate(stepLog);
            }
            else
            {
                stepLogOp.Value.EndTime = stepStatus.EndTime;
                log.StepLogs.AddOrUpdate(stepLogOp.Value); // 更新已有记录
                stepLogOp.Value.StepOnceLogs.AddOrUpdate(stepOnceStatus);
            }

            await Task.CompletedTask;
        }
    }
}
