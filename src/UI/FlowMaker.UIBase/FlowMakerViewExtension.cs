using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Ty;
using Ty.ViewModels;

namespace System
{
    public static class FlowMakerViewExtension
    {
        public static void AddSingletonFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ICustomPageViewModel
        {
            serviceDescriptors.AddKeyedSingleton<ICustomPageInjectViewModel, TViewModel>(TViewModel.Category + ":" + TViewModel.Name);
            serviceDescriptors.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                var group = c.GetOrAddGroup(TViewModel.Category);

                group.CustomViewDefinitions.Add(TViewModel.GetDefinition());
            });
        }
        public static void AddTransientFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ICustomPageViewModel
        {
            serviceDescriptors.AddKeyedTransient<ICustomPageInjectViewModel, TViewModel>(TViewModel.Category + ":" + TViewModel.Name);
            serviceDescriptors.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
            serviceDescriptors.AddTransientView<TViewModel, TView>();

            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                var group = c.GetOrAddGroup(TViewModel.Category);

                group.CustomViewDefinitions.Add(TViewModel.GetDefinition());
            });
        }
    }
}
