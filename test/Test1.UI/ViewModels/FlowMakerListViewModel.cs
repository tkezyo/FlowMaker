using FlowMaker;
using FlowMaker.Services;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using Windows.Media.Devices;

namespace Test1.ViewModels
{
    public class FlowMakerListViewModel : ViewModelBase
    {
        private readonly IMessageBoxManager _messageBoxManager;

        public FlowMakerListViewModel( IMessageBoxManager messageBoxManager)
        {
            CreateCommand = ReactiveCommand.CreateFromTask(Create);
            this._messageBoxManager = messageBoxManager;
        }

        public ReactiveCommand<Unit, Unit> CreateCommand { get; }
        public async Task Create()
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            await vm.Load();
            await Task.CompletedTask;
            _messageBoxManager.Window.Handle(new FlowMaker.Services.ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).Subscribe();
            //HostScreen.Router.Navigate.Execute(Navigate<FlowMakerEditViewModel>(HostScreen));
        }
    }
}
