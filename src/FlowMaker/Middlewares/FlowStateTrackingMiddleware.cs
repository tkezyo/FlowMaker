namespace FlowMaker.Middlewares;

public class FlowStateTrackingMiddleware : IMiddleware<FlowContext>
{
    public static string Name => "状态管理";

    public async Task InvokeAsync(MiddlewareDelegate<FlowContext> next, FlowContext context, CancellationToken cancellationToken)
    {
        if (context.State != FlowState.Wait)
        {
            return;
        }
        try
        {
            context.Init();

            context.State = FlowState.Running;
            context.StartTime = DateTime.Now;
            await next(context, cancellationToken);

            await context.TaskCompletionSource.Task;
            context.EndTime = DateTime.Now;
            if (context.Finally)
            {
                context.State = FlowState.Error;
                context.Result.Success = false;
            }
            else
            {
                context.Result.Success = true;
                context.State = FlowState.Complete;
            }
        }
        catch (TaskCanceledException e)
        {
            context.Result.Success = false;
            context.Result.Exception = e;
            context.EndTime = DateTime.Now;
            context.State = FlowState.Cancel;
        }
        catch (Exception e)
        {
            context.Result.Success = false;
            context.Result.Exception = e;
            context.EndTime = DateTime.Now;
            context.State = FlowState.Error;
        }


        foreach (var item in context.FlowDefinition.Data)
        {
            if (!item.IsOutput)
            {
                continue;
            }
            var data = context.Data.Lookup(item.Name);
            if (data.HasValue)
            {
                context.Result.Data.Add(new FlowResultData { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value.Value });
            }
        }


    }
}
