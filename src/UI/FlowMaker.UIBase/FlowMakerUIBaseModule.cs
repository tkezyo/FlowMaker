using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ty;

namespace FlowMaker
{
    public class FlowMakerUIBaseModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerModule>();
        }
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddSingleton<MemoryFlowLogProvider>();
            hostApplicationBuilder.Services.AddSingleton<IFlowLogReader>(c => c.GetRequiredService<MemoryFlowLogProvider>());
            hostApplicationBuilder.Services.AddSingleton<IFlowLogWriter>(c => c.GetRequiredService<MemoryFlowLogProvider>());


            hostApplicationBuilder.Services.AddKeyedSingleton<IStepOnceMiddleware, StepOnceMiddleware>("iio");
            hostApplicationBuilder.Services.AddKeyedSingleton<IStepOnceMiddleware, DebugMiddleware>("debug");

            hostApplicationBuilder.Services.AddScoped<MonitorMiddleware>();
            hostApplicationBuilder.Services.AddKeyedScoped<IStepOnceMiddleware, MonitorMiddleware>("monitor", (c, k) => c.GetRequiredService<MonitorMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IFlowMiddleware, MonitorMiddleware>("monitor", (c, k) => c.GetRequiredService<MonitorMiddleware>());

            hostApplicationBuilder.Services.AddKeyedScoped<IFlowMiddleware, LogFlowMiddleware>("log");
            hostApplicationBuilder.Services.AddKeyedScoped<IStepMiddleware, LogStepMiddleware>("log");
            hostApplicationBuilder.Services.AddKeyedScoped<IStepOnceMiddleware, LogStepOnceMiddleware>("log");
            hostApplicationBuilder.Services.AddKeyedScoped<IEventMiddleware, LogEventMiddleware>("log");
            return Task.CompletedTask;
        }
    }
}
