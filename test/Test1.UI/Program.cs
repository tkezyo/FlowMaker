using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
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
           .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
           .WriteTo.File($"logs/log.txt", rollingInterval: RollingInterval.Day)
              .WriteTo.Map(
          keyPropertyName: "TestName",
          configure: (testName, wt) => wt.File(new CompactJsonFormatter(), $"logs/{testName}.log"))
           .Enrich.FromLogContext();


            Log.Logger = configuration.CreateLogger();

            try
            {
                var host = await IModule.CreateApplicationHost<Test1UIModule>(args) ?? throw new Exception();

                Thread thread = new(async () =>
                {
                    await host.RunAsync();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch (Exception)
            {
                Log.CloseAndFlush();
            }



        }
    }
}
