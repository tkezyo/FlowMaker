using FlowMaker.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia;
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
            hostApplicationBuilder.Services.AddTransientCustomPageView<FlowMakerDebugViewModel, FlowMakerDebugView>();
            IconProvider.Current
          .Register<FontAwesomeIconProvider>();

            return Task.CompletedTask;
        }
    }
}
