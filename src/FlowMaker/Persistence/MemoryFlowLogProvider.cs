using System.Collections.Concurrent;

namespace FlowMaker.Persistence
{
    public class MemoryFlowLogProvider : IFlowLogger
    {
        public ConcurrentDictionary<Guid, List<FlowLog>> Logs { get; set; } = [];
        public async Task<FlowLog[]> GetFlowLog(Guid id)
        {
            Logs.TryGetValue(id, out var log);
            await Task.CompletedTask;
            return log?.ToArray() ?? [];
        }
        public async Task LogFlow(FlowContext flowContext, Exception? exception = null)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var logs))
            {
                logs = [];
                Logs.TryAdd(flowContext.FlowIds[0], logs);
            }
            var last = logs.LastOrDefault();
            if (last is null || !last.EndTime.HasValue)
            {
                var log = new FlowLog
                {
                    Id = flowContext.FlowIds[0],
                    Category = flowContext.FlowDefinition.Category,
                    Name = flowContext.FlowDefinition.Name,
                    StartTime = DateTime.Now,
                    CurrentIndex = flowContext.CurrentIndex,
                    ErrorIndex = flowContext.ErrorIndex,
                    // Inputs = flowContext.Inputs.Select(x => new NameValue(x.Name, x.Value)).ToList(),
                    //  Middlewares = flowContext.Middlewares,
                    //StepLogs = flowContext.StepIds.Select(x => new StepLog
                    //{
                    //    FlowIds = flowContext.FlowIds,
                    //    StepId = x,
                    //    StartTime = DateTime.Now
                    //}).ToList()
                };
                logs.Add(log);
            }
            else
            {
                last.EndTime = DateTime.Now;
            }


            await Task.CompletedTask;
        }



        public Task LogMiddleware(Guid id, List<string> middlewares)
        {
            if (!Logs.TryGetValue(id, out var logs))
            {
                return Task.CompletedTask;
            }
            var log = logs.Last();
            log.Middlewares = middlewares;
            return Task.CompletedTask;
        }

        public async Task LogEvent(FlowContext flowContext, string eventName, string? eventData)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var logs))
            {
                return;
            }
            var log = logs.Last();

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
            var log = logs.Last();
            var stepId = string.Join(",", flowContext.FlowIds) + "," + flowStep.Id + "," + flowContext.CurrentIndex + "," + flowContext.ErrorIndex;
            if (!log.StepLogs.TryGetValue(stepId, out var stepLog))
            {
                stepLog = new StepLog
                {
                    FlowIds = flowContext.FlowIds,
                    StepName = flowStep.DisplayName,
                    StepId = flowStep.Id,
                    StartTime = DateTime.Now,

                };
                stepLog.StepOnceLogs.Add(stepOnceStatus);
                log.StepLogs.TryAdd(stepId, stepLog);
            }
            else
            {

                stepLog.EndTime = stepStatus.EndTime;
            }

            await Task.CompletedTask;
        }
    }
}
