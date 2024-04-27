using FlowMaker.ViewModels;
using FlowMaker.Views;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using Ty;

namespace FlowMaker
{
    public class FlowMakerWpfModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerUIBaseModule>();
            AddDepend<TyWPFBaseModule>();
        }

        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddTransientView<FlowMakerMainViewModel, FlowMakerMainView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerCustomPageViewModel, FlowMakerCustomPageView>();
            hostApplicationBuilder.Services.AddSingletonView<FlowMakerMonitorViewModel, FlowMakerMonitorView>();
            hostApplicationBuilder.Services.AddTransientCustomPageView<FlowMakerDebugViewModel, FlowMakerDebugView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerSelectViewModel, FlowMakerSelectView>();
            hostApplicationBuilder.Services.AddTransientView<FlowMakerLogViewModel, FlowMakerLogView>();


            return Task.CompletedTask;
        }
    }
}
