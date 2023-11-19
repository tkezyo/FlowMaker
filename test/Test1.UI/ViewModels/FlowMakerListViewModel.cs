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
using System.Reactive.Linq;
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
            CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
            this._messageBoxManager = messageBoxManager;
            this._flowManager = flowManager;
        }

        public ReactiveCommand<FlowDefinitionInfoViewModel?, Unit> CreateCommand { get; }
        public async Task Create(FlowDefinitionInfoViewModel? flowDefinitionInfoViewModel)
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            await vm.Load(flowDefinitionInfoViewModel?.Category, flowDefinitionInfoViewModel?.Name);
            await Task.CompletedTask;
            _messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            {
                LoadMenus();
            });
            //HostScreen.Router.Navigate.Execute(Navigate<FlowMakerEditViewModel>(HostScreen));
        }

        public override async Task Activate()
        {
            LoadMenus();
            await Task.CompletedTask;
        }

        public ObservableCollection<FlowDefinitionInfoViewModel> FlowMenus { get; set; } = [];
        public void LoadMenus()
        {
            FlowMenus.Clear();
            var categories = _flowManager.LoadFlowCategories();
            foreach (var category in categories)
            {
                var flows = _flowManager.LoadFlows(category);
                foreach (var flow in flows)
                {
                    FlowMenus.Add(new FlowDefinitionInfoViewModel(flow.Category, flow.Name, flow.CreationTime, flow.ModifyTime));
                }
            }

        }
    }

    public class FlowDefinitionInfoViewModel(string category, string name, DateTime creationTime, DateTime modifyTime) : ReactiveObject
    {
        public string Category { get; set; } = category;

        public string Name { get; set; } = name;
        public DateTime CreationTime { get; set; } = creationTime;
        public DateTime ModifyTime { get; set; } = modifyTime;
    }
}
