using DynamicData;

namespace FlowMaker.Middlewares;

public class StepGroupTrackingMiddleware(IServiceProvider serviceProvider) : IMiddleware<StepGroupContext>
{

    public static string Name => "状态管理";
    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Status.StartTime = DateTime.Now;
            context.Status.State = StepState.Start;

            var repeat = await IDataConverterInject.GetValue(context.Step.Repeat, serviceProvider, context.FlowContext, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            var retry = await IDataConverterInject.GetValue(context.Step.Retry, serviceProvider, context.FlowContext, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            var isFinally = await IDataConverterInject.GetValue(context.Step.Finally, serviceProvider, context.FlowContext, s => bool.TryParse(s, out var r) && r, cancellationToken);
            var errorHandling = await IDataConverterInject.GetValue(context.Step.ErrorHandling, serviceProvider, context.FlowContext, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);

            context.Status.Repeat = repeat;
            context.Status.Retry = retry;
            context.Status.Finally = isFinally;
            context.Status.ErrorHandling = errorHandling;

            context.FlowContext.StepState.AddOrUpdate(context.Status);

            await next(context, cancellationToken);

            if (context.Status.State != StepState.Skip)
            {
                context.Status.EndTime = DateTime.Now;
                context.Status.State = StepState.Complete;
                context.FlowContext.StepState.AddOrUpdate(context.Status);
            }
        }
        catch (Exception e)
        {
            context.Status.EndTime = DateTime.Now;
            context.Status.State = StepState.Error;
            context.FlowContext.StepState.AddOrUpdate(context.Status);

            if (e is TaskCanceledException)
            {
                context.FlowContext.TaskCompletionSource.SetCanceled(cancellationToken);
                return;
            }

            switch (context.Status.ErrorHandling)
            {
                case ErrorHandling.Skip:
                    break;
                case ErrorHandling.Finally:
                    context.FlowContext.Finally = true;
                    break;
                case ErrorHandling.Terminate:
                    context.FlowContext.TaskCompletionSource.TrySetException(e);
                    break;
                default:
                    break;
            }
        }

        context.FlowContext.ExecuteStepSubject.OnNext(new ExecuteStepEvent
        {
            Type = EventType.PreStep,
            StepId = context.Step.Id,
            Context = context.FlowContext
        });
    }
}
