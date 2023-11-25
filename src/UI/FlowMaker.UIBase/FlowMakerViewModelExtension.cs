using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace System;

public static class FlowMakerViewModelExtension
{
    public static void AddSingletonView<TViewModel, TView>(this IServiceCollection services)
        where TView : class, IViewFor<TViewModel>
        where TViewModel : class, IFlowMakerRoutableViewModel
    {
        services.AddSingleton<TViewModel>();
        services.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
    }
    public static void AddTransientView<TViewModel, TView>(this IServiceCollection services)
        where TView : class, IViewFor<TViewModel>
        where TViewModel : class, IFlowMakerRoutableViewModel
    {
        services.AddTransient<TViewModel>();
        services.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
    }
}
