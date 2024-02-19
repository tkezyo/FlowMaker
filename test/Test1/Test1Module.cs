using FlowMaker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ty;

namespace Test1
{
    public class Test1Module : ModuleBase
    {
        public Test1Module()
        {

        }
        public override void DependsOn()
        {
            AddDepend<FlowMakerModule>();
        }
        public override Task ConfigureServices(IServiceCollection serviceDescriptors, IConfigurationRoot configurationRoot)
        {
            serviceDescriptors.AddFlowStep<Flow1>();
            serviceDescriptors.AddFlowStep<Flow2>();
            serviceDescriptors.AddFlowStep<Flow3>();
            serviceDescriptors.AddFlowStep<MyClass>();
            serviceDescriptors.AddFlowStep<TestFlow1>();
            serviceDescriptors.AddFlowConverter<ValueConverter>();
            serviceDescriptors.AddFlowOption<PortProvider>();
            return Task.CompletedTask;
        }
    }
}
