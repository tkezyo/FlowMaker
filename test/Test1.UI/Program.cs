using FlowMaker;
using FlowMaker.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System;
using System.Reactive.Linq;
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

                services.AddBaseViews();
                services.AddTransientFlowView<ChatViewModel, ChatView>();
                services.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
                services.AddTransientView<FlowMakerListViewModel, FlowMakerListView>();
                services.AddTransientView<FlowMakerConfigEditViewModel, FlowMakerConfigEditView>();
                services.AddTransientView<FlowMakerSelectViewModel, FlowMakerSelectView>();
                services.AddTransient<FlowRunner>();
                services.AddSingleton<FlowManager>();

                services.AddFlowStep<Flow1>();
                services.AddFlowStep<Flow2>();
                services.AddFlowConverter<ValueConverter>();
                services.AddAutoMapper(typeof(ConfigProfile).Assembly);
                services.Configure<ViewForMatch>(options =>
                {
                    options.Add(Test1UIViewLocatorMatcher.Match);
                });
                services.Configure<PageOptions>(options =>
                {
                    options.FirstLoadPage = typeof(FlowMakerListViewModel);
                });
                services.Configure<FlowMakerOption>(options =>
                {
                    options.Sections.Add("设备1");
                    options.Sections.Add("设备2");
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
}
