using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Windows;

namespace Ty;

public class AppViewLocator : IViewLocator
{
    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
    {
        if (viewModel is null)
        {
            throw new Exception($"没有找到{viewModel}对应的视图");
        }
        var typeName = viewModel.GetType().FullName;
        var v = TyApp.ServiceProvider.GetRequiredKeyedService<IViewFor>(typeName);

        if (v is not null)
        {
            if (v is FrameworkElement uc)
            {
                uc.DataContext = viewModel;
                v.ViewModel = viewModel;

                return v;
            }
            return v;
        }
        throw new Exception($"没有找到{viewModel}对应的视图");
    }
}
