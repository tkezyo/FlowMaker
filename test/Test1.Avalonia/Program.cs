using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using System;
using Ty;

namespace Test1.Avalonia
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
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
            var hostBuilder = Host.CreateDefaultBuilder();

            hostBuilder.ConfigureServices(async services =>
            {
                await IModule.ConfigureServices<Test1AvaloniaModule>(services);
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
