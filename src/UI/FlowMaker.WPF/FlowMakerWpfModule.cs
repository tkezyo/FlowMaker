using FlowMaker.ViewModels;
using FlowMaker.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public override Task ConfigureServices(IServiceCollection serviceDescriptors, IConfigurationRoot configurationRoot)
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
