using FlowMaker;
using Microsoft.Extensions.DependencyInjection;

namespace Test1
{
    public class Test1Module : ModuleBase
    {
        public Test1Module()
        {

        }
        public override void DependsOn()
        {
            AddDepand<FlowMakerModule>();
        }
        public override Task ConfigureServices(IServiceCollection serviceDescriptors)
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
