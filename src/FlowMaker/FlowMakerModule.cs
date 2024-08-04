using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowMaker;

public class FlowMakerModule : Ty.ModuleBase
{
    public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
    {
        //hostApplicationBuilder.Services.AddTransient<FlowRunner>();
        hostApplicationBuilder.Services.AddSingleton<FlowManager>();
        hostApplicationBuilder.Services.AddSingleton<IFlowProvider, FileFlowProvider>();

        hostApplicationBuilder.Services.AddScopedMiddleware<FlowExecuteMiddleware>(FlowExecuteMiddleware.Name, FlowExecuteMiddleware.Name);
        hostApplicationBuilder.Services.AddScopedMiddleware<FlowStateTrackingMiddleware>(FlowStateTrackingMiddleware.Name, FlowStateTrackingMiddleware.Name);
        hostApplicationBuilder.Services.AddScopedMiddleware<StepGroupExecuteMiddleware>(StepGroupExecuteMiddleware.Name, StepGroupExecuteMiddleware.Name);
        hostApplicationBuilder.Services.AddScopedMiddleware<StepGroupTrackingMiddleware>(StepGroupTrackingMiddleware.Name, StepGroupTrackingMiddleware.Name);
        hostApplicationBuilder.Services.AddScopedMiddleware<StepExecuteMiddleware>(StepExecuteMiddleware.Name, StepExecuteMiddleware.Name);
        hostApplicationBuilder.Services.AddScopedMiddleware<StepTrackingMiddleware>(StepTrackingMiddleware.Name, StepTrackingMiddleware.Name);


        return Task.CompletedTask;
    }
}
