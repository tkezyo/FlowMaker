using FlowMaker.Persistence;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker.Middlewares;

public class MonitorMiddleware(IFlowProvider flowProvider) : IMiddleware<FlowContext>, IMiddleware<StepGroupContext>, IMiddleware<StepContext>
{
    public static string Name => "调试页面";

    public int TotalCount { get; set; } = -1;
    public double CompleteCount { get; set; }
    public double Percent { get; set; }
    /// <summary>
    /// 步骤变化
    /// </summary>
    public ReplaySubject<MonitorStepOnceMessage> StepChange { get; set; } = new();
    /// <summary>
    /// 百分比变化
    /// </summary>
    public ReplaySubject<double> PercentChange { get; set; } = new(1);

    public async Task InvokeAsync(MiddlewareDelegate<FlowContext> next, FlowContext context, CancellationToken cancellationToken)
    {
        if (context.FlowIds.Length == 1)
        {

            var definition = await flowProvider.LoadFlowDefinitionAsync(context.ConfigDefinition.Category, context.ConfigDefinition.Name);
            if (definition is null)
            {
                return;
            }

            TotalCount = 0;

            async Task SetFlowStepAsync(IFlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (item.Type == StepType.Normal)
                    {
                        TotalCount++;
                    }
                    else if (item.Type == StepType.Embedded && flowDefinition is FlowDefinition fde)
                    {
                        var stepDefinition = fde.EmbeddedFlows.First(c => c.StepId == item.Id);

                        await SetFlowStepAsync(stepDefinition);
                    }
                    else
                    {
                        var stepDefinition = await flowProvider.GetStepDefinitionAsync(item.Category, item.Name);
                        if (stepDefinition is FlowDefinition fd)
                        {
                            await SetFlowStepAsync(fd);
                        }
                    }
                }
            }

            await SetFlowStepAsync(definition);

            CompleteCount = 0;
            Percent = 0;

            StepChange.Dispose();
            StepChange = new ReplaySubject<MonitorStepOnceMessage>();
            PercentChange.Dispose();
            PercentChange = new ReplaySubject<double>(1);

        }
        if (!PercentChange.IsDisposed)
        {
            PercentChange.OnNext(Percent);
        }

        MessageBus.Current.SendMessage(new MonitorMessage(context, TotalCount));

        await next(context, cancellationToken);

        await Task.Delay(10, cancellationToken);//解决视图过快，无法停止的问题

        MessageBus.Current.SendMessage(new MonitorMessage(context, TotalCount));
        await Task.CompletedTask;
    }

    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.Status, null, context.FlowContext.FlowIds, context.Step));
        }

        await next(context, cancellationToken);
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.Status, null, context.FlowContext.FlowIds, context.Step));
        }
    }

    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        if (context.StepStatus.State == StepOnceState.Start && context.StepStatus.StartTime.HasValue)
        {
            CompleteCount += 0.5;
            Percent = (double)CompleteCount / TotalCount * 100;

        }

        if (context.StepStatus.State == StepOnceState.Skip)
        {
            CompleteCount += 1;
            Percent = (double)CompleteCount / TotalCount * 100;
        }

        if (!PercentChange.IsDisposed)
        {
            PercentChange.OnNext(Percent);
        }
        if (!StepChange.IsDisposed)
        {
            StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.StepGroupStatus, context.StepStatus, context.FlowContext.FlowIds, context.Step));
        }

        await next(context, cancellationToken);
    }
}

public class MonitorMessage(FlowContext context, int totalCount)
{
    public FlowContext Context { get; set; } = context;
    public int TotalCount { get; set; } = totalCount;
}

public class MonitorStepOnceMessage(FlowContext flowContext, StepGroupStatus stepGroupStatus, StepStatus? stepStatus, Guid[] flowIds, FlowStep step)
{
    public FlowContext FlowContext { get; set; } = flowContext;
    public StepGroupStatus StepGroupStatus { get; } = stepGroupStatus;
    public StepStatus? StepStatus { get; set; } = stepStatus;
    public Guid[] FlowIds { get; set; } = flowIds;
    public FlowStep Step { get; set; } = step;
}
