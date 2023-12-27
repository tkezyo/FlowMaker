using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ty
{
    public class WpfHostedService<TApplication, TMainWindow> : IHostedService
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
