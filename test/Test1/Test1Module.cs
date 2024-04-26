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
            hostApplicationBuilder.Services.AddFlowOption<PortProvider>();
            hostApplicationBuilder.Services.AddCaesarModeFlowStep();
            hostApplicationBuilder.Services.AddScoped<CaesarMode>();
            hostApplicationBuilder.Services.AddICaesarModeFlowStep();

            return Task.CompletedTask;
        }
    }
}
