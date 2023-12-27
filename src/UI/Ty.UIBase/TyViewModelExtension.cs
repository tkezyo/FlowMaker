using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Ty.ViewModels;

namespace Ty;

public static class TyViewModelExtension
{
    public static void AddSingletonView<TViewModel, TView>(this IServiceCollection services)
        where TView : class, IViewFor<TViewModel>
        where TViewModel : class, ITyRoutableViewModel
    {
        services.AddSingleton<TViewModel>();
        services.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
    }
    public static void AddTransientView<TViewModel, TView>(this IServiceCollection services)
        where TView : class, IViewFor<TViewModel>
        where TViewModel : class, ITyRoutableViewModel
    {
        services.AddTransient<TViewModel>();
        services.AddKeyedTransient<IViewFor, TView>(typeof(TViewModel).FullName);
    }
}
