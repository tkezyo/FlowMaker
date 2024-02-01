using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using Ty;
using Ty.AvaloniaBase.Views;
using Ty.ViewModels;

namespace Test1.Avalonia
{
    public partial class App : Application
    {
        public App()
        {
            
        }
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = TyApp.ServiceProvider.GetRequiredService<MainWindow>();
              
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}