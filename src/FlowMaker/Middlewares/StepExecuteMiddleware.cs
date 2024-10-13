using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Ty;

namespace FlowMaker.Middlewares;

public class StepExecuteMiddleware(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option, IFlowProvider flowProvider, FlowManager flowManager) : IMiddleware<StepContext>
{
    public static string Name => "执行";
    private readonly FlowMakerOption _flowMakerOption = option.Value;
    public async Task InvokeAsync(MiddlewareDelegate<StepContext> next, StepContext context, CancellationToken cancellationToken)
    {
        var timeOut = IStepInject.GetValue(context.Step.Timeout, context.FlowContext, s => int.TryParse(s, out var r) ? r : 0);
        //超时策略
        if (timeOut > 0)
        {
            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(timeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
            await timeoutPolicy.ExecuteAsync(async c => await ExecuteStep(context, c), cancellationToken);
        }
        else
        {
            await ExecuteStep(context, cancellationToken);
        }

    }

    /// <summary>
    /// 执行步骤
    /// </summary>
    /// <param name="stepContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task ExecuteStep(StepContext stepContext, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!stepContext.Step.SubFlowId.HasValue)
        {
            var stepDefinition = _flowMakerOption.GetStep(stepContext.Step.Category, stepContext.Step.Name)
                ?? throw new Exception($"未找到{stepContext.Step.Category}，{stepContext.Step.Name}定义");

            var stepObj = serviceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(stepContext, serviceProvider, cancellationToken);
        }
        else
        {

            if (!stepContext.Step.SubFlowId.HasValue)
            {
                throw new Exception("SubFlowId 为空");
            }

            var subFlowDefinition = await flowProvider.LoadFlowDefinitionAsync(stepContext.Step.SubFlowId.Value);

            if (subFlowDefinition is null)
            {
                throw new Exception("未找到SubFlow定义");
            }

            var config = new ConfigDefinition(stepContext.Step.Category, stepContext.Step.Name);
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = IStepInject.GetValue(stepContext.Step.Inputs.First(v => v.Name == item.Name), stepContext.FlowContext, item.DefaultValue);
                config.Data.Add(new NameValue(item.Name, value));
            }
            config.FlowMiddlewares = stepContext.FlowContext.FlowMiddlewares;
            config.StepGroupMiddlewares = stepContext.FlowContext.StepGroupMiddlewares;
            config.StepMiddlewares = stepContext.FlowContext.StepMiddlewares;

            var context = flowManager.GetFlowContext([.. stepContext.FlowContext.FlowIds, stepContext.Step.Id]);

            if (context is null)
            {
                config.FlowMiddlewares = stepContext.FlowContext.FlowMiddlewares;
                config.StepGroupMiddlewares = stepContext.FlowContext.StepGroupMiddlewares;
                config.StepMiddlewares = stepContext.FlowContext.StepMiddlewares;
                context = new(subFlowDefinition, config, [.. stepContext.FlowContext.FlowIds, stepContext.Step.Id], stepContext.CurrentIndex, stepContext.ErrorIndex, stepContext.FlowContext.Index, stepContext.FlowContext.Logs, stepContext.FlowContext.WaitEvents);
                context.Init();
            }


            var builder = new MiddlewareBuilder<FlowContext>(serviceProvider);

            foreach (var item in context.FlowMiddlewares)
            {
                builder.Use(item);
            }

            var application = builder.Build();

            await application.Invoke(context, cancellationToken);

            foreach (var item in context.Result.Data)
            {
                IStepInject.SetValue(stepContext.Step.Outputs.First(v => v.Name == item.Name), item.Value, stepContext.FlowContext);
            }
        }
    }
}
