using FlowMaker.Middlewares;
using FlowMaker.ViewModels;
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
            hostApplicationBuilder.Services.AddSingletonMiddleware<DebugMiddleware>(DebugMiddleware.Name, DebugMiddleware.Name);

            hostApplicationBuilder.Services.AddScoped<MonitorModel>();
            hostApplicationBuilder.Services.AddScoped<MonitorMiddleware>();
            hostApplicationBuilder.Services.AddScoped<MonitorEndMiddleware>();

            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<StepContext>, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<FlowContext>, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<StepGroupContext>, MonitorMiddleware>(MonitorMiddleware.Name, (c, k) => c.GetRequiredService<MonitorMiddleware>());

            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<StepContext>, MonitorEndMiddleware>(MonitorEndMiddleware.Name, (c, k) => c.GetRequiredService<MonitorEndMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<FlowContext>, MonitorEndMiddleware>(MonitorEndMiddleware.Name, (c, k) => c.GetRequiredService<MonitorEndMiddleware>());
            hostApplicationBuilder.Services.AddKeyedScoped<IMiddleware<StepGroupContext>, MonitorEndMiddleware>(MonitorEndMiddleware.Name, (c, k) => c.GetRequiredService<MonitorEndMiddleware>());

            hostApplicationBuilder.Services.Configure<FlowMakerOption>(c =>
            {
                c.FlowMiddlewares.Add(new NameValue(MonitorMiddleware.Name, MonitorMiddleware.Name));
                c.StepGroupMiddlewares.Add(new NameValue(MonitorMiddleware.Name, MonitorMiddleware.Name));
                c.StepMiddlewares.Add(new NameValue(MonitorMiddleware.Name, MonitorMiddleware.Name));
                c.FlowMiddlewares.Add(new NameValue(MonitorEndMiddleware.Name, MonitorEndMiddleware.Name));
                c.StepGroupMiddlewares.Add(new NameValue(MonitorEndMiddleware.Name, MonitorEndMiddleware.Name));
                c.StepMiddlewares.Add(new NameValue(MonitorEndMiddleware.Name, MonitorEndMiddleware.Name));
            });

            hostApplicationBuilder.Services.Configure<MenuOptions>(options =>
            {
                options.Menus.Add(new MenuInfo { DisplayName = "工作流", GroupName = "FlowMaker", Name = "Menu.FlowMaker", Color = Color.Black, ViewModel = typeof(FlowMakerMainViewModel) });
                options.Menus.Add(new MenuInfo { DisplayName = "编辑", GroupName = "FlowMaker", Name = "Menu.FlowMakerEdit", Color = Color.Black });
            });

            return Task.CompletedTask;
        }
    }
}
