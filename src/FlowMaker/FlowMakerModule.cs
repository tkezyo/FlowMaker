using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker;

public class FlowMakerModule : Ty.ModuleBase
{
    public override Task ConfigureServices(IServiceCollection serviceDescriptors)
    {
        serviceDescriptors.AddTransient<FlowRunner>();
        serviceDescriptors.AddSingleton<FlowManager>();
        serviceDescriptors.AddTransient<IFlowProvider, FileFlowProvider>();
        return Task.CompletedTask;
    }
}
