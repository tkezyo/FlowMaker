using FlowMaker.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker;

public class FlowMakerModule : Ty.ModuleBase
{
    public override Task ConfigureServices(IServiceCollection serviceDescriptors, IConfigurationRoot configurationRoot)
    {
        serviceDescriptors.AddTransient<FlowRunner>();
        serviceDescriptors.AddSingleton<FlowManager>();
        serviceDescriptors.AddTransient<IFlowProvider, FileFlowProvider>();
        return Task.CompletedTask;
    }
}
