using FlowMaker;
using FlowMaker.ViewModels;
using FlowMaker.Views;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using Test1.ViewModels;
using Volo.Abp;
using Volo.Abp.Modularity.PlugIns;

namespace Test1.UI;

public record LogFilter(string Name, string PropertyName, ScalarValue Value, string FileName, RollingInterval RollingInterval);
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;

    protected override async void OnStartup(StartupEventArgs e)
    {
        MainWindow = new MainWindow();
        MainWindow.DataContext = new MainWindowViewModel() { Title = "模拟器" };
        MainWindow.Show();

        List<LogFilter> filters = new List<LogFilter>();
        for (int i = 0; i < 4; i++)
        {
            filters.Add(new LogFilter("协议日志", "ClientName", new ScalarValue("Client" + i), $"Logs/CommandClient-{i}-.txt", RollingInterval.Day));
        }
        var configuration = new LoggerConfiguration()
#if DEBUG
               .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
               .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
               .Enrich.FromLogContext();
        foreach (var item in filters)
        {
            configuration = configuration.WriteTo.Logger(lg =>
            {
                lg.Filter.ByIncludingOnly(Matching.WithProperty<string>(item.PropertyName, p => p.Equals(item.Value.Value))).WriteTo.File(item.FileName, rollingInterval: item.RollingInterval);
            });
        }


        var groupFilter = filters.GroupBy(c => c.PropertyName);
        configuration = configuration.WriteTo.Logger(lg =>
        {
            lg.Filter.ByExcluding(e =>
            {
                foreach (var filter in groupFilter)
                {
                    if (e.Properties.TryGetValue(filter.Key, out var propertyValue))
                    {
                        if (filter.Any(c => c.Value.Equals(propertyValue)))
                        {
                            return true;
                        }
                    }
                }
                return false;

            }).WriteTo.File("Logs/logs-.txt", rollingInterval: RollingInterval.Day);
        });


        Log.Logger = configuration.CreateLogger();
        try
        {
            Log.Information("Starting WPF host.");

            _abpApplication = await AbpApplicationFactory.CreateAsync<UIModule>(options =>
            {
                options.UseAutofac();
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"PlugIns");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                options.PlugInSources.AddFolder(path);
                options.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            });

            await _abpApplication.InitializeAsync();

            FlowMakerApp.ServiceProvider = _abpApplication.ServiceProvider;

            RxApp.MainThreadScheduler.Schedule(async () =>
            {
                if (MainWindow.DataContext is MainWindowViewModel windowViewModel)
                {
                    var login = _abpApplication.ServiceProvider.GetRequiredService<FlowMakerListViewModel>();
                    //var login = _abpApplication.ServiceProvider.GetRequiredService<SerialPortViewModel>();
                    await windowViewModel.Router.Navigate.Execute(login);
                }
            });

        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_abpApplication != null)
        {
            await _abpApplication.ShutdownAsync();
        }
        Log.CloseAndFlush();
    }
}

