﻿using FlowMaker.ViewModels;
using FlowMaker;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ty;
using Ty.AvaloniaBase.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using FlowMaker.Middlewares;

namespace Test1.Avalonia
{
    public class Test1AvaloniaModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<Test1Module>();
            AddDepend<FlowMakerAvaloniaModule>();
        }
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddSingleton<App>();
            hostApplicationBuilder.Services.AddTransient<MainWindow>();
            hostApplicationBuilder.Services.AddHostedService<AvaloniaHostedService<App, MainWindow>>();
            hostApplicationBuilder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            //serviceDescriptors.AddTransientCustomPageView<ChatViewModel, ChatView>();
            //serviceDescriptors.AddCustomLogView<CustomLogViewModel, CustomLogView>();

            hostApplicationBuilder.Services.Configure<PageOptions>(options =>
            {
                options.FirstLoadPage = typeof(FlowMakerMainViewModel);
                options.Title = "牛马指挥官";
            });
            hostApplicationBuilder.Services.Configure<FlowMakerOption>(options =>
            {
                options.FlowRootDir = "D:\\FlowMaker\\Flow";
                options.DebugPageRootDir = "D:\\FlowMaker\\DebugPage";
                options.Section = "设备1";
                options.Middlewares.Add(new NameValue("测试中间件", "iio"));
                options.DefaultMiddlewares.Add(new NameValue("监控", MonitorMiddleware.Name));
                options.DefaultMiddlewares.Add(new NameValue("调试", DebugMiddleware.Name));
                options.DefaultMiddlewares.Add(new NameValue("日志", "log"));
            });

            return Task.CompletedTask;
        }
    }
}
