﻿using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using Windows.Media.Devices;

namespace Test1.ViewModels
{
    public class FlowMakerListViewModel : RoutableViewModelBase
    {
        public FlowMakerListViewModel()
        {
            CreateCommand = ReactiveCommand.Create(Create);
        }

        public ReactiveCommand<Unit, Unit> CreateCommand { get; }
        public void Create()
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            MessageBox.Window.Handle(new FlowMaker.Services.ModalInfo("", vm) { OwnerTitle = null }).Subscribe();
            //HostScreen.Router.Navigate.Execute(Navigate<FlowMakerEditViewModel>(HostScreen));
        }
    }
}
