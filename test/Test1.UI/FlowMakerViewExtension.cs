using FlowMaker;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Test1.ViewModels;

namespace System
{
    public static class FlowMakerViewExtension
    {
        public static void AddSingletonFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ICustomPageViewModel
        {
            serviceDescriptors.AddKeyedSingleton<ICustomPageInjectViewModel, TViewModel>(TViewModel.ViewName);
            serviceDescriptors.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                c.CustomViews.Add(TViewModel.ViewName);
            });
        }
        public static void AddTransientFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ICustomPageViewModel
        {
            serviceDescriptors.AddKeyedTransient<ICustomPageInjectViewModel, TViewModel>(TViewModel.ViewName);
            serviceDescriptors.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                c.CustomViews.Add(TViewModel.ViewName);
            });
        }
    }
}
