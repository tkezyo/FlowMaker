﻿using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowMaker;

public class FlowMakerModule : Ty.ModuleBase
{
    public override Task ConfigureServices(IHostApplicationBuilder hostApplicationBuilder)
    {
        //hostApplicationBuilder.Services.AddTransient<FlowRunner>();
        hostApplicationBuilder.Services.AddSingleton<FlowManager>();
        hostApplicationBuilder.Services.AddSingleton<IFlowProvider, FileFlowProvider>();
        return Task.CompletedTask;
    }
}
