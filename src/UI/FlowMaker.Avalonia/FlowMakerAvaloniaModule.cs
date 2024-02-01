using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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

        public override Task ConfigureServices(IServiceCollection serviceDescriptors)
        {
            serviceDescriptors.AddTransientView<FlowMakerMainViewModel, FlowMakerMainView>();
            serviceDescriptors.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
            serviceDescriptors.AddTransientView<FlowMakerCustomPageViewModel, FlowMakerCustomPageView>();
            serviceDescriptors.AddSingletonView<FlowMakerMonitorViewModel, FlowMakerMonitorView>();
            serviceDescriptors.AddTransientCustomPageView<FlowMakerDebugViewModel, FlowMakerDebugView>();
            serviceDescriptors.AddTransientView<FlowMakerSelectViewModel, FlowMakerSelectView>();
            serviceDescriptors.AddTransientView<FlowMakerLogViewModel, FlowMakerLogView>();


            return Task.CompletedTask;
        }
    }
}
