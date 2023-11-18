using FlowMaker.Services;
using FlowMaker.ViewModels;
using FlowMaker.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FlowMaker
{
    public static class FlowMakerWPFBaseExtension
    {
        public static void AddViews(this IServiceCollection services)
        {
            services.AddSingleton<IMessageBoxManager, MessageBoxManager>();

            services.AddSingletonView<LayoutViewModel, LayoutView>();
            services.AddTransientView<ModalDialogViewModel, ModalDialog>();
            services.AddTransientView<PromptDialogViewModel, PromptDialog>();
        }
    }
}
