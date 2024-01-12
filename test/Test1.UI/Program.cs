using FlowMaker;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using System;
using System.Reactive.Linq;
using Ty;

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

            hostBuilder.ConfigureServices(async services =>
            {
                await IModule.ConfigureServices<Test1UIModule>(services);
            });

            var host = hostBuilder.Build();
            TyApp.ServiceProvider = host.Services;
            using (host)
            {
                host.Start();
                host.WaitForShutdown();
            }
        }
    }
}
