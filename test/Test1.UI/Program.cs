using FlowMaker;
using FlowMaker.ViewModels;
using FlowMaker.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Test1.UI;
using Test1.ViewModels;
using Test1.Views;

namespace Test1
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var configuration = new LoggerConfiguration()
#if DEBUG
           .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
           .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
           .Enrich.FromLogContext();


            Log.Logger = configuration.CreateLogger();


            var hostBuilder = Host.CreateDefaultBuilder(args);

            hostBuilder.ConfigureServices(services =>
            {
                // 配置服务和依赖注入
                services.AddSingleton<App>();
                services.AddTransient<MainWindow>();
                services.AddHostedService<WpfHostedService<App, MainWindow>>();
                services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

                services.AddViews();
                services.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
                services.AddTransientView<FlowMakerListViewModel, FlowMakerListView>();

                services.AddFlowStep<Flow1>();
                services.AddFlowStep<Flow2>();
                services.AddFlowConverter<ValueConverter>();
                services.AddSingleton<FlowManager>();

                services.Configure<ViewForMatch>(options =>
                {
                    options.Add(Test1UIViewLocatorMatcher.Match);
                });
                services.Configure<PageOptions>(options =>
                {
                    options.FirstLoadPage = typeof(FlowMakerListViewModel);
                });
            });

            var host = hostBuilder.Build();
            FlowMakerApp.ServiceProvider = host.Services;
            using (host)
            {
                host.Start();
                host.WaitForShutdown();
            }
        }
    }

    class WpfHostedService<TApplication, TMainWindow> : IHostedService
        where TApplication : Application
        where TMainWindow : Window
    {
        public WpfHostedService(TApplication application, TMainWindow mainWindow, IHostApplicationLifetime hostApplicationLifetime)
        {
            this.application = application;
            this.mainWindow = mainWindow;
            hostApplicationLifetime.ApplicationStopping.Register(application.Shutdown);
        }

        private readonly TApplication application;
        private readonly TMainWindow mainWindow;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            application.Run(mainWindow);
          
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
