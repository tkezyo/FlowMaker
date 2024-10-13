using DynamicData;

namespace FlowMaker.Middlewares;

public class StepGroupTrackingMiddleware : IMiddleware<StepGroupContext>
{
    public static string Name => "状态管理";
    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Status.StartTime = DateTime.Now;
            context.Status.State = StepGroupState.Start;

            var repeat = IStepInject.GetValue(context.Step.Repeat, context.FlowContext, s => int.TryParse(s, out var r) ? r : 0);
            var retry = IStepInject.GetValue(context.Step.Retry, context.FlowContext, s => int.TryParse(s, out var r) ? r : 0);
            var isFinally = IStepInject.GetValue(context.Step.Finally, context.FlowContext, s => bool.TryParse(s, out var r) && r);
            var errorHandling = IStepInject.GetValue(context.Step.ErrorHandling, context.FlowContext, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip);

            context.Status.Repeat = repeat;
            context.Status.Retry = retry;
            context.Status.Finally = isFinally;
            context.Status.ErrorHandling = errorHandling;

            context.FlowContext.StepState.AddOrUpdate(context.Status);

            await next(context, cancellationToken);

            if (context.Status.State != StepGroupState.Skip)
            {
                context.Status.EndTime = DateTime.Now;
                context.Status.State = StepGroupState.Complete;
                context.FlowContext.StepState.AddOrUpdate(context.Status);
            }
        }
        catch (Exception e)
        {
            context.Status.EndTime = DateTime.Now;
            context.Status.State = StepGroupState.Error;
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
