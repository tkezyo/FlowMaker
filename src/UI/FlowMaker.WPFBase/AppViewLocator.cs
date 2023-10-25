using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using System;
using System.Windows;

namespace FlowMaker
{
    public class AppViewLocator : IViewLocator
    {
        public ViewForMatch? Matcher { get; set; }
        public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
        {
            if (Matcher is null)
            {
                var option = FlowMakerApp.ServiceProvider.GetRequiredService<IOptions<ViewForMatch>>();
                Matcher = option.Value;
            }
            if (viewModel is null)
            {
                throw new Exception($"没有找到{viewModel}对应的视图");
            }
            foreach (var item in Matcher.Match)
            {
                var v = item(viewModel);
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
            }
            throw new Exception($"没有找到{viewModel}对应的视图");
        }
    }
}
