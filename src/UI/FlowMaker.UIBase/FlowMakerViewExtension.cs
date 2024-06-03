using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Ty;

namespace System;

public static class FlowMakerViewExtension
{
    public static void AddCustomLogView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
        where TView : class, IViewFor<TViewModel>
        where TViewModel : class, ILogViewModel
    {
        serviceDescriptors.AddKeyedTransient<ILogInjectViewModel, TViewModel>(TViewModel.Name);
        serviceDescriptors.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
        serviceDescriptors.AddTransientView<TViewModel, TView>();

        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            c.CustomLogViews.Add(TViewModel.Name);
        });
    }
}
