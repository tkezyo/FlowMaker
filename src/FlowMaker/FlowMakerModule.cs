using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowMaker;

public class FlowMakerModule : Ty.ModuleBase
{
    public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
    {
        hostApplicationBuilder.Services.AddTransient<FlowRunner>();
        hostApplicationBuilder.Services.AddSingleton<FlowManager>();
        hostApplicationBuilder.Services.AddTransient<IFlowProvider, FileFlowProvider>();

        hostApplicationBuilder.Services.AddKeyedScoped<IFlowMiddleware, LogFlowMiddleware>("log");
        hostApplicationBuilder.Services.AddKeyedScoped<IStepMiddleware, LogStepMiddleware>("log");
        hostApplicationBuilder.Services.AddKeyedScoped<IStepOnceMiddleware, LogStepOnceMiddleware>("log");
        hostApplicationBuilder.Services.AddKeyedScoped<IEventMiddleware, LogEventMiddleware>("log");
        hostApplicationBuilder.Services.AddKeyedScoped<ILogMiddleware, LogMiddleware>("log");
        return Task.CompletedTask;
    }
}
