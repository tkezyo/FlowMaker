using FlowMaker.Persistence;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace FlowMaker.Middlewares;

public class MonitorModel
{
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
}

public class MonitorEndMiddleware(MonitorModel monitorModel) : IMiddleware<FlowContext>, IMiddleware<StepGroupContext>, IMiddleware<StepContext>
{
    public static string Name => "调试页面结束";
    public async Task InvokeAsync(MiddlewareDelegate<FlowContext> next, FlowContext context, CancellationToken cancellationToken)
    {
        await next(context, cancellationToken);

        MessageBus.Current.SendMessage(new MonitorMessage(context, monitorModel.TotalCount));
    }
    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        await next(context, cancellationToken);
        if (!monitorModel.StepChange.IsDisposed)
        {
            monitorModel.StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.Status, null, context.FlowContext.FlowIds, context.Step));
        }
    }

    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        await next(context, cancellationToken);

        if (!monitorModel.StepChange.IsDisposed)
        {
            monitorModel.StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.StepGroupStatus, context.StepStatus, context.FlowContext.FlowIds, context.Step));
        }
    }


}

public class MonitorMiddleware(IFlowProvider flowProvider, MonitorModel monitorModel) : IMiddleware<FlowContext>, IMiddleware<StepGroupContext>, IMiddleware<StepContext>
{
    private readonly IFlowProvider _flowProvider = flowProvider;

    public static string Name => "调试页面";

    public MonitorModel Model { get; } = monitorModel;
    public async Task InvokeAsync(MiddlewareDelegate<FlowContext> next, FlowContext context, CancellationToken cancellationToken)
    {
        if (context.FlowIds.Length == 1)
        {

            var definition = await _flowProvider.LoadFlowDefinitionAsync(context.ConfigDefinition.FlowId);
            if (definition is null)
            {
                return;
            }

            Model.TotalCount = 0;

            async Task SetFlowStepAsync(IFlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (!item.SubFlowId.HasValue)
                    {
                        Model.TotalCount++;
                    }
                    else
                    {
                        var stepDefinition = await _flowProvider.GetStepDefinitionAsync(item.Category, item.Name);
                        if (stepDefinition is FlowDefinition fd)
                        {
                            await SetFlowStepAsync(fd);
                        }
                    }
                }
            }

            await SetFlowStepAsync(definition);

            Model.CompleteCount = 0;
            Model.Percent = 0;

            Model.StepChange.Dispose();
            Model.StepChange = new ReplaySubject<MonitorStepOnceMessage>();
            Model.PercentChange.Dispose();
            Model.PercentChange = new ReplaySubject<double>(1);

        }
        if (!Model.PercentChange.IsDisposed)
        {
            Model.PercentChange.OnNext(Model.Percent);
        }

        MessageBus.Current.SendMessage(new MonitorMessage(context, Model.TotalCount));
        await Task.Delay(10, cancellationToken);//解决视图过快，无法停止的问题

        await next(context, cancellationToken);

        await Task.CompletedTask;
    }

    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        if (!Model.StepChange.IsDisposed)
        {
            Model.StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.Status, null, context.FlowContext.FlowIds, context.Step));
        }

        await next(context, cancellationToken);
    }

    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        if (context.StepStatus.State == StepState.Start && context.StepStatus.StartTime.HasValue)
        {
            Model.CompleteCount += 0.5;
            Model.Percent = (double)Model.CompleteCount / Model.TotalCount * 100;

        }

        if (context.StepStatus.State == StepState.Skip)
        {
            Model.CompleteCount += 1;
            Model.Percent = (double)Model.CompleteCount / Model.TotalCount * 100;
        }

        if (!Model.PercentChange.IsDisposed)
        {
            Model.PercentChange.OnNext(Model.Percent);
        }
        if (!Model.StepChange.IsDisposed)
        {
            Model.StepChange.OnNext(new MonitorStepOnceMessage(context.FlowContext, context.StepGroupStatus, context.StepStatus, context.FlowContext.FlowIds, context.Step));
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
