using DynamicData;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.Services;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Windows.Media.Devices;

namespace Test1.ViewModels
{
    public class FlowMakerListViewModel : ViewModelBase
    {
        private readonly IMessageBoxManager _messageBoxManager;
        private readonly FlowManager _flowManager;

        public FlowMakerListViewModel(IMessageBoxManager messageBoxManager, FlowManager flowManager)
        {
            CreateCommand = ReactiveCommand.CreateFromTask(Create);
            this._messageBoxManager = messageBoxManager;
            this._flowManager = flowManager;
        }

        public ReactiveCommand<Unit, Unit> CreateCommand { get; }
        public async Task Create()
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            await vm.Load();
            await Task.CompletedTask;
            _messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).Subscribe();
            //HostScreen.Router.Navigate.Execute(Navigate<FlowMakerEditViewModel>(HostScreen));
        }

        public override async Task Activate()
        {
            LoadMenus();
            await Task.CompletedTask;
        }

        public ObservableCollection<FlowMenuViewModel> FlowMenus { get; set; } = [];
        public void LoadMenus()
        {
            FlowMenus.Clear();
            var categories = _flowManager.LoadFlowCategories();
            foreach (var category in categories)
            {
                var flowMenu = new FlowMenuViewModel(category);
                var names = _flowManager.LoadFlows(category);
                foreach (var name in names)
                {
                    flowMenu.Names.Add(new NameValue(category, name));
                }
                FlowMenus.Add(flowMenu);
            }

        }
    }

    public class FlowMenuViewModel(string category) : ReactiveObject
    {
        public string Category { get; set; } = category;

        public ObservableCollection<NameValue> Names { get; set; } = [];
    }
}
