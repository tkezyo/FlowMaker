using DynamicData;
using Microsoft.Extensions.Logging;

namespace FlowMaker.Middlewares;

public class StepGroupExecuteMiddleware(IServiceProvider serviceProvider) : IMiddleware<StepGroupContext>
{
    public static string Name => "执行";
    public async Task InvokeAsync(MiddlewareDelegate<StepGroupContext> next, StepGroupContext context, CancellationToken cancellationToken)
    {
        StepGroupState state = StepGroupState.Start;
        int errorIndex = 0;
        for (int i = 0; i < context.Status.Repeat; i++)
        {
            string? skipReason = null;
            if (context.FlowContext.Finally)
            {
                if (!context.Status.Finally)
                {
                    skipReason = "Finally";

                    state = StepGroupState.Skip;
                }
            }
            else
            {
                foreach (var item2 in context.Step.Conditions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    if (!item2.Execute)
                    {
                        continue;
                    }
                    var result = CheckStep(context.FlowContext, item2.Name);
                    if (result != item2.IsTrue)
                    {
                        skipReason = item2.Name;
                        state = StepGroupState.Skip;
                        break;
                    }
                }
            }

            void Log(StepStatus stepStatus, string log, LogLevel logLevel = LogLevel.Information)
            {
                var info = new LogInfo(log, logLevel, DateTime.Now, context.Step.Id, stepStatus.Index);
                context.FlowContext.Logs.Add(info);
            }
            while (true)
            {
                StepStatus once = new(i, errorIndex, context.FlowContext.Index, Log, c => context.Status.Logs.AddOrUpdate(c));
                if (state == StepGroupState.Skip)
                {
                    once.State = StepState.Skip;
                    Log(once, $"Step {context.Step.Name} Skip, Reason: {skipReason}", LogLevel.Information);
                }
                List<string> additionalConditions = [];
                once.ExtraData.Add(StepStatus.AdditionalConditions, additionalConditions);
                foreach (var item2 in context.Step.Conditions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    var result = CheckStep(context.FlowContext, item2.Name);
                    if (result == item2.IsTrue)
                    {
                        additionalConditions.Add(item2.Name);
                    }
                }
                try
                {
                    StepContext stepContext = new(context.Step, context.FlowContext, context.Status, once);


                    var builder = new MiddlewareBuilder<StepContext>(serviceProvider);

                    foreach (var item in context.FlowContext.StepMiddlewares)
                    {
                        builder.Use(item);
                    }

                    var application = builder.Build();

                    await application.Invoke(stepContext, cancellationToken);

                    await next(context, cancellationToken);
                    break;
                }
                catch (Exception e)
                {
                    errorIndex++;

                    if (context.Status.Retry >= errorIndex)
                    {
                        continue;
                    }
                    throw new Exception("step group failed", e);
                }
            }
        }

    }
    protected bool CheckStep(FlowContext flowContext, string key)
    {
        var entity = flowContext.Data.Lookup(key);
        if (entity == null)
        {
            if (bool.TryParse(entity.Value.Value, out var r))
            {
                return r;
            }
        }

        return false;
    }
}
