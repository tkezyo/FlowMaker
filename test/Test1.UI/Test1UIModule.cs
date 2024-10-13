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
using FlowMaker.Persistence;
using SharpVectors.Renderers.Wpf;
using SharpVectors.Converters;
using System.Windows.Media;
using System.Windows.Controls;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlowMaker.Persistence.EntityFramework;
using FlowMaker.Persistence.SQLite;
using Test1.Data;
using Microsoft.EntityFrameworkCore;

namespace Test1
{
    public class Test1UIModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<Test1Module>();
            AddDepend<FlowMakerWpfModule>();
            AddDepend<FlowMakerUIBaseModule>();
            AddDepend<FlowMakerPersistenceSQLiteModule>();
        }
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {

            hostApplicationBuilder.Services.AddSingleton<App>();
            hostApplicationBuilder.Services.AddTransient<MainWindow>();
            hostApplicationBuilder.Services.AddTransient<UIDbContext>();
            hostApplicationBuilder.Services.AddHostedService<WpfHostedService<App, MainWindow>>();
            hostApplicationBuilder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));


            //hostApplicationBuilder.Services.AddSingleton<IFlowLogger, MemoryFlowLogProvider>();

            hostApplicationBuilder.Services.AddTransientCustomPageView<ChatViewModel, ChatView>();
            hostApplicationBuilder.Services.AddCustomLogView<CustomLogViewModel, CustomLogView>();
            hostApplicationBuilder.Services.AddTransientView<LoadingViewModel, Loading>();

            hostApplicationBuilder.Services.Configure<PageOptions>(options =>
            {
                options.FirstLoadPage = typeof(LoadingViewModel);
                options.Title = "牛马指挥官";

                //WpfDrawingSettings settings = new WpfDrawingSettings();

                //// 获取当前程序集
                //Assembly assembly = Assembly.GetAssembly(typeof(Test1UIModule));

                //// 构建资源名称
                //string resourceName = "Test1.PURE.svg"; // 注意: 这里的路径需要根据实际情况调整
                //// 从嵌入的资源中读取SVG文件
                //using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                //{
                //    if (stream == null) throw new InvalidOperationException("无法找到嵌入的资源.");

                //    // 使用FileSvgReader从Stream中读取SVG
                //    FileSvgReader reader = new FileSvgReader(settings);
                //    DrawingGroup drawing = reader.Read(stream);

                //    // 创建一个DrawingImage并将DrawingGroup设置为其源
                //    DrawingImage drawingImage = new DrawingImage(drawing);


                //    Image image = new Image();
                //    // 创建一个Image控件并将DrawingImage设置为其源
                //    image.Source = drawingImage;
                //    //image.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                //    //image.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                //    options.Loading = image;
                //}
            });
            hostApplicationBuilder.Services.Configure<FlowMakerOption>(options =>
            {
                options.DebugPageRootDir = "D:\\FlowMaker\\DebugPage";
                options.Section = "设备1";
                options.AutoRun = false;
                options.CanDebug = true;
                options.MaxColCount = 5;
                //options.Middlewares.Add(new NameValue("测试中间件", "iio"));
                //options.DefaultMiddlewares.Add(new NameValue("监控", "monitor"));
                //options.DefaultMiddlewares.Add(new NameValue("调试", "debug"));
            });

            hostApplicationBuilder.Services.Configure<SQLiteOptions>(o =>
            {
                o.ConnectString = "Data Source=FlowMaker.db";
            });
            return Task.CompletedTask;
        }

        public override Task PostConfigureServices(IServiceProvider serviceProvider)
        {
            using var db = serviceProvider.GetRequiredService<UIDbContext>();
            db.Database.Migrate();

            return base.PostConfigureServices(serviceProvider);
        }
    }
}
