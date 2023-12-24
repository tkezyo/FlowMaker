using FlowMaker;
using FlowMaker.Persistence;
using FlowMaker.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
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

            RxApp.DefaultExceptionHandler = new MyCoolObservableExceptionHandler();
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
                services.AddTransientView<FlowMakerMainViewModel, FlowMakerMainView>();
                services.AddTransientView<FlowMakerEditViewModel, FlowMakerEditView>();
                services.AddTransientView<FlowMakerCustomPageViewModel, FlowMakerCustomPageView>();
                services.AddTransientView<FlowMakerListViewModel, FlowMakerListView>();
                services.AddSingletonView<FlowMakerMonitorViewModel, FlowMakerMonitorView>();
                //  services.AddTransientView<FlowMakerConfigEditViewModel, FlowMakerConfigEditView>();
                services.AddTransientView<FlowMakerSelectViewModel, FlowMakerSelectView>();
                services.AddTransient<FlowRunner>();
                services.AddTransient<IFlowProvider, FileFlowProvider>();
                services.AddSingleton<FlowManager>();
                services.AddKeyedSingleton<IStepOnceMiddleware, StepOnceMiddleware>("iio");
                services.AddKeyedScoped<IStepOnceMiddleware, MonitorStepOnceMiddleware>("monitor");
                services.AddKeyedScoped<IStepOnceMiddleware, DebugMiddleware>("debug");
                services.AddKeyedScoped<IFlowMiddleware, MonitorFlowMiddleware>("monitor");

                services.AddFlowStep<Flow1>();
                services.AddFlowStep<Flow2>();
                services.AddFlowStep<MyClass>();
                services.AddFlowStep<TestFlow1>();
                services.AddFlowConverter<ValueConverter>();
                services.AddFlowOption<PortProvider>();
                services.AddAutoMapper(typeof(ConfigProfile).Assembly);

                services.Configure<PageOptions>(options =>
                {
                    options.FirstLoadPage = typeof(FlowMakerMainViewModel);
                });
                services.Configure<FlowMakerOption>(options =>
                {
                    options.RootDir = "D:\\FlowMaker";
                    options.Sections.Add("设备1");
                    options.Sections.Add("设备2");
                    options.Middlewares.Add(new FlowMaker.Models.NameValue("测试中间件", "iio"));
                    options.DefaultMiddlewares.Add(new FlowMaker.Models.NameValue("监控", "monitor"));
                    options.DefaultMiddlewares.Add(new FlowMaker.Models.NameValue("调试", "debug"));
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
