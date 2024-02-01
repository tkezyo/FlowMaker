using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Test1.Avalonia;

namespace Test1
{
    public class AvaloniaHostedService2<TApplication, TMainWindow> : IHostedService
          where TApplication : Application, new()
          where TMainWindow : Window, new()
    {
        public AvaloniaHostedService2(IHostApplicationLifetime hostApplicationLifetime)
        {
            //hostApplicationLifetime.ApplicationStopping.Register(application.);
            //this.mainWindow = mainWindow;
        }

        private readonly TApplication application;
        private readonly TMainWindow mainWindow;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var thread = new Thread(static state =>
            {
                var builder = BuildAvaloniaApp();
                _ = builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnMainWindowClose);
            });

            thread.Start(this);
            return Task.CompletedTask;
        }
        public static AppBuilder BuildAvaloniaApp()
          => AppBuilder.Configure<TApplication>()
              .UsePlatformDetect()
              .WithInterFont()
              .LogToTrace()
              .UseReactiveUI();
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}