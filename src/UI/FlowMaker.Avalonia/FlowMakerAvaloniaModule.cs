using FlowMaker.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ty;

namespace FlowMaker
{
    public class FlowMakerAvaloniaModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerUIBaseModule>();
            AddDepend<TyAvaloniaBaseModule>();
        }

        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddTransientView<FlowMakerMainViewModel, FlowMakerMainView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
            hostApplicationBuilder.Services.AddSingletonView<FlowMakerMonitorViewModel, FlowMakerMonitorView>();
            hostApplicationBuilder.Services.AddTransientCustomPageView<FlowMakerDebugViewModel, FlowMakerDebugView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerSelectViewModel, FlowMakerSelectView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerLogViewModel, FlowMakerLogView>();


            return Task.CompletedTask;
        }
    }
}
