using AutoMapper;
using FlowMaker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test1.ViewModels;

namespace Test1
{
    public class ConfigProfile: Profile
    {
        public ConfigProfile()
        {
            CreateMap<SpikeTabViewModel, SpikeTab>();
            CreateMap<SpikeBoxViewModel, SpikeBox>();
            CreateMap<SpikeActionViewModel, SpikeAction>();
            CreateMap<SpikeTab, SpikeTabViewModel>();
            CreateMap<SpikeBox, SpikeBoxViewModel>();
            CreateMap<SpikeAction, SpikeActionViewModel>();

            //CreateMap<WorkflowOutput, SpikeActionOutputViewModel>();
            //CreateMap<SpikeActionOutputViewModel, WorkflowOutput>();
            //CreateMap<SpikeActionInputViewModel, WorkflowInput>();
            //CreateMap<WorkflowInput, SpikeActionInputViewModel>();


            CreateMap<SpikeResizable, SpikeResizableViewModel>();
            CreateMap<SpikeResizableViewModel, SpikeResizable>();

            CreateMap<SpikeMoveable, SpikeMoveableViewModel>();
            CreateMap<SpikeMoveableViewModel, SpikeMoveable>();

            CreateMap<SpikeMoveAndResizable, SpikeBoxResizableViewModel>();
            CreateMap<SpikeBoxResizableViewModel, SpikeMoveAndResizable>();

            CreateMap<SpikeMoveAndResizableViewModel, SpikeMoveAndResizable>();
            CreateMap<SpikeMoveAndResizable, SpikeMoveAndResizableViewModel>();

            // Use CreateMap... Etc.. here (Profile methods are the same as configuration methods)
        }
    }
}
