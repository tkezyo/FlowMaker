using FlowMaker;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test1.ViewModels;

namespace System
{
    public static class FlowMakerViewExtension
    {
        public static void AddSingletonFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ISpikeViewModel
        {
            serviceDescriptors.AddKeyedSingleton<ISpikeInjectViewModel, TViewModel>(TViewModel.ViewName);
            serviceDescriptors.AddTransient<IViewFor<TViewModel>, TView>();
            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                c.CustomViews.Add(TViewModel.ViewName);
            });
        }
        public static void AddTransientFlowView<TViewModel, TView>(this IServiceCollection serviceDescriptors)
            where TView : class, IViewFor<TViewModel>
            where TViewModel : class, ISpikeViewModel
        {
            serviceDescriptors.AddKeyedTransient<ISpikeInjectViewModel, TViewModel>(TViewModel.ViewName);
            serviceDescriptors.AddTransient<IViewFor<TViewModel>, TView>();
            serviceDescriptors.Configure<FlowMakerOption>(c =>
            {
                c.CustomViews.Add(TViewModel.ViewName);
            });
        }
    }
}
