using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test1.UI;
using Test1.ViewModels;
using Test1.Views;
using Ty.Views;
using Ty;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Test1
{
    public class Test1UIModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<Test1Module>();
            AddDepend<FlowMakerWpfModule>();
        }
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {

            hostApplicationBuilder.Services.AddSingleton<App>();
            hostApplicationBuilder.Services.AddTransient<MainWindow>();
            hostApplicationBuilder.Services.AddHostedService<WpfHostedService<App, MainWindow>>();
            hostApplicationBuilder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

          hostApplicationBuilder.Services.AddTransientCustomPageView<ChatViewModel, ChatView>();
            hostApplicationBuilder.Services.AddCustomLogView<CustomLogViewModel, CustomLogView>();

            hostApplicationBuilder.Services.AddAutoMapper(typeof(ConfigProfile).Assembly);

            hostApplicationBuilder.Services.Configure<PageOptions>(options =>
            {
                options.FirstLoadPage = typeof(FlowMakerMainViewModel);
                options.Title = "牛马指挥官";
            });
            hostApplicationBuilder.Services.Configure<FlowMakerOption>(options =>
            {
                options.FlowRootDir = "D:\\FlowMaker\\Flow";
                options.DebugPageRootDir = "D:\\FlowMaker\\DebugPage";
                options.CustomPageRootDir = "D:\\FlowMaker\\CustomPage";
                options.Section = "设备1";
                options.Middlewares.Add(new FlowMaker.NameValue("测试中间件", "iio"));
                options.DefaultMiddlewares.Add(new FlowMaker.NameValue("监控", "monitor"));
                options.DefaultMiddlewares.Add(new FlowMaker.NameValue("调试", "debug"));
                options.DefaultMiddlewares.Add(new FlowMaker.NameValue("日志", "log"));
            });

            return Task.CompletedTask;
        }
    }
}
