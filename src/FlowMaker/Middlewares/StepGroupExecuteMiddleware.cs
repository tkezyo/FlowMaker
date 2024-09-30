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
                foreach (var item2 in context.Step.Ifs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    (bool result, string reason) = await CheckStep(context.FlowContext, context.Step, item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        skipReason = reason;
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
                StepStatus once = new(i, errorIndex, context.FlowContext.Index, Log, c => context.Status.OnceLogs.AddOrUpdate(c));
                if (state == StepGroupState.Skip)
                {
                    once.State = StepState.Skip;
                    Log(once, $"Step {context.Step.Name} Skip, Reason: {skipReason}", LogLevel.Information);
                }
                List<string> additionalConditions = [];
                once.ExtraData.Add(StepStatus.AdditionalConditions, additionalConditions);
                foreach (var item2 in context.Step.AdditionalConditions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    (bool result, string name) = await CheckStep(context.FlowContext, context.Step, item2.Key, cancellationToken);
                    if (result == item2.Value)
                    {
                        additionalConditions.Add(name);
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
    protected async Task<(bool, string)> CheckStep(FlowContext flowContext, FlowStep flowStep, Guid convertId, CancellationToken cancellationToken)
    {
        var checker = flowContext.Checkers.FirstOrDefault(c => c.Id == convertId) ?? flowStep.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
        var result = await IDataConverterInject.GetValue(checker, serviceProvider, flowContext, s => bool.TryParse(s, out var r) && r, cancellationToken);
        return (result, checker.Name);
    }
}
