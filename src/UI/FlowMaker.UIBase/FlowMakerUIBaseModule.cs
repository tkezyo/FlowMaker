using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Drawing;
using Ty;

namespace FlowMaker
{
    public class FlowMakerUIBaseModule : ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerModule>();
        }
        public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
        {
            hostApplicationBuilder.Services.AddKeyedSingleton<IStepOnceMiddleware, DebugMiddleware>(DebugMiddleware.Name);

            hostApplicationBuilder.Services.AddScoped<MonitorMiddleware>();

            hostApplicationBuilder.Services.AddKeyedScoped<IStepOnceMiddleware, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IFlowMiddleware, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IStepMiddleware, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());

            hostApplicationBuilder.Services.Configure<MenuOptions>(options =>
            {
                options.Menus.Add(new MenuInfo { DisplayName = "工作流", GroupName = "FlowMaker", Name = "Menu.FlowMaker", Color = Color.Black, ViewModel = typeof(FlowMakerMainViewModel) });
                options.Menus.Add(new MenuInfo { DisplayName = "编辑", GroupName = "FlowMaker", Name = "Menu.FlowMakerEdit", Color = Color.Black });
            });

            return Task.CompletedTask;
        }
    }
}
