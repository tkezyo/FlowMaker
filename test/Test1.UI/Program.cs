using FlowMaker;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ty;

namespace Test1
{
    class Program
    {
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


            var host = await IModule.CreateApplicationHost<Test1UIModule>(args) ?? throw new Exception();

            Thread thread = new(async () =>
            {
                await host.RunAsync();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();


        }
    }
}
