namespace FlowMaker.Middlewares;

public class StepTrackingMiddleware : IMiddleware<StepContext>
{
    public static string Name => "状态管理";

    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.StepStatus.StartTime = DateTime.Now;
            context.StepStatus.State = StepState.Start;
            context.StepStatus.Update.Invoke(context.StepStatus);

            await next(context, cancellationToken);

            context.StepStatus.EndTime = DateTime.Now;
            context.StepStatus.State = StepState.Complete;
            context.StepStatus.Update.Invoke(context.StepStatus);
        }
        catch (Exception e)
        {
            context.StepStatus.EndTime = DateTime.Now;
            context.StepStatus.State = StepState.Error;
            context.StepStatus.Update.Invoke(context.StepStatus);

            throw new Exception("Step failed", e);
        }
    }
}
