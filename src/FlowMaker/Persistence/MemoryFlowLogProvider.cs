using FlowMaker.Models;
using System.Collections.Concurrent;

namespace FlowMaker.Persistence
{
    public class MemoryFlowLogProvider : IFlowLogReader, IFlowLogWriter
    {
        public ConcurrentDictionary<Guid, FlowLog> Logs { get; set; } = [];
        public Task<FlowLog?> GetFlowLog(Guid id)
        {
            Logs.TryGetValue(id, out var log);
            return Task.FromResult(log);
        }
        public async Task LogFlow(FlowContext flowContext)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var log))
            {
                log = new FlowLog
                {
                    Id = flowContext.FlowIds[0],
                    Category = flowContext.FlowDefinition.Category,
                    Name = flowContext.FlowDefinition.Name,
                    StartTime = DateTime.Now,
                    // Inputs = flowContext.Inputs.Select(x => new NameValue(x.Name, x.Value)).ToList(),
                    //  Middlewares = flowContext.Middlewares,
                    //StepLogs = flowContext.StepIds.Select(x => new StepLog
                    //{
                    //    FlowIds = flowContext.FlowIds,
                    //    StepId = x,
                    //    StartTime = DateTime.Now
                    //}).ToList()
                };

                Logs.TryAdd(flowContext.FlowIds[0], log);
            }
            else
            {
                log.EndTime = DateTime.Now;
            }


            await Task.CompletedTask;
        }

        public Task LogMiddleware(Guid id, List<string> middlewares)
        {
            if (!Logs.TryGetValue(id, out var log))
            {
                return Task.CompletedTask;
            }
            log.Middlewares = middlewares;
            return Task.CompletedTask;
        }

        public async Task LogEvent(FlowContext flowContext, string eventName, string? eventData)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var log))
            {
                return;
            }

            log.Events.Add(new EventLog
            {
                EventName = eventName,
                EventData = eventData,
                Time = DateTime.Now
            });

            await Task.CompletedTask;
        }

        public async Task LogStep(FlowContext flowContext, FlowStep flowStep, StepStatus stepStatus, StepOnceStatus stepOnceStatus)
        {
            if (!Logs.TryGetValue(flowContext.FlowIds[0], out var log))
            {
                return;
            }
            var stepId = string.Join(",", flowContext.FlowIds) + "," + flowStep.Id;
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
                if (stepStatus.Complete)
                {
                    stepLog.EndTime = DateTime.Now;
                }
            }

            await Task.CompletedTask;
        }
    }
}
