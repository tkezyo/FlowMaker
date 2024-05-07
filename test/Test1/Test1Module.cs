using FlowMaker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddFlowStep<Flow1>();
            hostApplicationBuilder.Services.AddFlowStep<Flow2>();
            hostApplicationBuilder.Services.AddFlowStep<Flow3>();
            hostApplicationBuilder.Services.AddFlowStep<MyClass>();
            hostApplicationBuilder.Services.AddFlowStep<TestFlow1>();
            hostApplicationBuilder.Services.AddFlowConverter<ValueConverter>();
            hostApplicationBuilder.Services.AddFlowConverter<BoolConverter>();
            hostApplicationBuilder.Services.AddFlowOption<PortProvider>();
            hostApplicationBuilder.Services.AddCaesarModeFlowStep();
            hostApplicationBuilder.Services.AddScoped<CaesarMode>();
            hostApplicationBuilder.Services.AddICaesarModeFlowStep();

            hostApplicationBuilder.Services.AddITestStepFlowStep();
            hostApplicationBuilder.Services.AddKeyedScoped<ITestStep, TestStep1>("Test1");
            hostApplicationBuilder.Services.AddKeyedScoped<ITestStep, TestStep2>("Test2");

            hostApplicationBuilder.Services.Configure<ITestStepInstanceOption>(c =>
            {
                c.Instances.Add(new FlowMaker.NameValue("Test1", "Test1"));
                c.Instances.Add(new FlowMaker.NameValue("Test2", "Test2"));
            });

            return Task.CompletedTask;
        }
    }
}
