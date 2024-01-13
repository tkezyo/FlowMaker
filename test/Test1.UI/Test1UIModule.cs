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

namespace Test1
{
    public class Test1UIModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<Test1Module>();
            AddDepend<FlowMakerWpfModule>();
        }
        public override Task ConfigureServices(IServiceCollection serviceDescriptors)
        {

            serviceDescriptors.AddSingleton<App>();
            serviceDescriptors.AddTransient<MainWindow>();
            serviceDescriptors.AddHostedService<WpfHostedService<App, MainWindow>>();
            serviceDescriptors.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            serviceDescriptors.AddTransientCustomPageView<ChatViewModel, ChatView>();
            serviceDescriptors.AddCustomLogView<CustomLogViewModel, CustomLogView>();

            serviceDescriptors.AddAutoMapper(typeof(ConfigProfile).Assembly);

            serviceDescriptors.Configure<PageOptions>(options =>
            {
                options.FirstLoadPage = typeof(FlowMakerMainViewModel);
                options.Title = "牛马指挥官";
            });
            serviceDescriptors.Configure<FlowMakerOption>(options =>
            {
                options.FlowRootDir = "D:\\FlowMaker\\Flow";
                options.DebugPageRootDir = "D:\\FlowMaker\\DebugPage";
                options.CustomPageRootDir = "D:\\FlowMaker\\CustomPage";
                options.Section = "设备1";
                options.Middlewares.Add(new FlowMaker.Models.NameValue("测试中间件", "iio"));
                options.DefaultMiddlewares.Add(new FlowMaker.Models.NameValue("监控", "monitor"));
                options.DefaultMiddlewares.Add(new FlowMaker.Models.NameValue("调试", "debug"));
                options.DefaultMiddlewares.Add(new FlowMaker.Models.NameValue("日志", "log"));
            });

            return Task.CompletedTask;
        }
    }
}
