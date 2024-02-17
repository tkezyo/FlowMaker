using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using System;
using System.Threading.Tasks;
using Ty;

namespace Test1.Avalonia
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static async Task Main(string[] args)
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

            var host = await IModule.CreateHost<Test1AvaloniaModule>(args) ?? throw new Exception();

            await host.RunAsync();
        }
    }
}
