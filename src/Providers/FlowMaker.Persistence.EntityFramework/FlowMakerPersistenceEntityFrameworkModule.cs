using FlowMaker.Persistence.EntityFramework.Models;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker.Persistence.EntityFramework
{
    public class FlowMakerPersistenceEntityFrameworkModule : Ty.ModuleBase
    {
        public override async Task ConfigureServices(IHostApplicationBuilder builder)
        {
            builder.Services.AddTransient<IFlowProvider, FlowProvider>();
            builder.Services.AddTransient<FlowMakerDbContext>();



            await Task.CompletedTask;
        }
    }
}
