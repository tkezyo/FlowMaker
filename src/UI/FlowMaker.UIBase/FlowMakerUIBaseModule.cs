using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Ty;

namespace FlowMaker
{
    public class FlowMakerUIBaseModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerModule>();
        }
        public override Task ConfigureServices(IServiceCollection serviceDescriptors)
        {
            serviceDescriptors.AddSingleton<MemoryFlowLogProvider>();
            serviceDescriptors.AddSingleton<IFlowLogReader>(c => c.GetRequiredService<MemoryFlowLogProvider>());
            serviceDescriptors.AddSingleton<IFlowLogWriter>(c => c.GetRequiredService<MemoryFlowLogProvider>());


            serviceDescriptors.AddKeyedSingleton<IStepOnceMiddleware, StepOnceMiddleware>("iio");
            serviceDescriptors.AddKeyedSingleton<IStepOnceMiddleware, DebugMiddleware>("debug");

            serviceDescriptors.AddScoped<MonitorMiddleware>();
            serviceDescriptors.AddKeyedScoped<IStepOnceMiddleware, MonitorMiddleware>("monitor", (c, k) => c.GetRequiredService<MonitorMiddleware>());
            serviceDescriptors.AddKeyedScoped<IFlowMiddleware, MonitorMiddleware>("monitor", (c, k) => c.GetRequiredService<MonitorMiddleware>());

            serviceDescriptors.AddKeyedScoped<IFlowMiddleware, LogFlowMiddleware>("log");
            serviceDescriptors.AddKeyedScoped<IStepMiddleware, LogStepMiddleware>("log");
            serviceDescriptors.AddKeyedScoped<IStepOnceMiddleware, LogStepOnceMiddleware>("log");
            serviceDescriptors.AddKeyedScoped<IEventMiddleware, LogEventMiddleware>("log");
            return Task.CompletedTask;
        }
    }
}
