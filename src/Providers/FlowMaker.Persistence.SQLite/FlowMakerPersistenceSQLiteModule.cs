using FlowMaker.Persistence.EntityFramework.Models;
using FlowMaker.Persistence.SQLite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowMaker.Persistence.EntityFramework
{
    public class FlowMakerPersistenceSQLiteModule : Ty.ModuleBase
    {
        public override void DependsOn()
        {
            AddDepend<FlowMakerPersistenceEntityFrameworkModule>();
        }
        public override async Task ConfigureServices(IHostApplicationBuilder builder)
        {
            builder.Services.AddTransient<IFlowMakerDbContextBuilder, FlowMakerDbContextBuilder>();

            await Task.CompletedTask;
        }
    }
}
