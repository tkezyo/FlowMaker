using AutoMapper;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.Persistence;
using FlowMaker.Services;
using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Test1.ViewModels
{
    public class FlowMakerListViewModel : ViewModelBase
    {
        private readonly IMessageBoxManager _messageBoxManager;
        private readonly FlowManager _flowManager;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly IFlowProvider _flowProvider;
        private readonly FlowMakerOption _flowMakerOption;

        public FlowMakerListViewModel(IMessageBoxManager messageBoxManager, FlowManager flowManager, IMapper mapper, IServiceProvider serviceProvider, IOptions<FlowMakerOption> options, IFlowProvider flowProvider)
        {
            CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
            RemoveCommand = ReactiveCommand.Create<FlowDefinitionInfoViewModel>(Remove);
            ConfigCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Config);
            DebugFlowCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(DebugFlow);
            EditConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(EditConfig);
            //RemoveConfigCommand = ReactiveCommand.Create<ConfigDefinitionInfoViewModel>(RemoveConfig);
            //RunCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(Run);

            _flowMakerOption = options.Value;
            this._messageBoxManager = messageBoxManager;
            this._flowManager = flowManager;
            _mapper = mapper;
            this._serviceProvider = serviceProvider;
            this._flowProvider = flowProvider;
        }

        public IList<MenuItemViewModel> InitMenu()
        {
            List<MenuItemViewModel> menus = [];
            menus.Add(new MenuItemViewModel("创建流程") { Command = CreateCommand });

            return menus;
        }

        #region 流程与配置
        public ReactiveCommand<FlowDefinitionInfoViewModel?, Unit> CreateCommand { get; }
        public async Task Create(FlowDefinitionInfoViewModel? flowDefinitionInfoViewModel)
        {
            //var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            //await vm.Load(flowDefinitionInfoViewModel?.Category, flowDefinitionInfoViewModel?.Name);
            //await Task.CompletedTask;
            //_messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            //{
            //    LoadFlowMenus();
            //});
        }
        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> RemoveCommand { get; }
        public void Remove(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            //_flowProvider.RemoveFlow(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
            LoadFlowMenus();
        }
        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> ConfigCommand { get; }
        public async Task Config(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            //var vm = Navigate<FlowMakerConfigEditViewModel>(HostScreen);
            //await vm.Load(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);

            //await _messageBoxManager.Modals.Handle(new ModalInfo("配置", vm, 400));
            LoadFlowMenus();
        }

        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> DebugFlowCommand { get; }
        public async Task DebugFlow(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            //var vm = _serviceProvider.GetRequiredService<FlowMakerMonitorViewModel>();
            //await vm.Load( flowDefinitionInfoViewModel.Name);
        }

        public override async Task Activate()
        {
            LoadFlowMenus();
            await Task.CompletedTask;
        }

        public ObservableCollection<FlowDefinitionInfoViewModel> FlowMenus { get; set; } = [];
        public void LoadFlowMenus()
        {
            FlowMenus.Clear();
            var categories = _flowProvider.LoadCategories();
            foreach (var category in categories)
            {
                var flows = _flowProvider.LoadFlows(category);
                foreach (var flow in flows)
                {
                    FlowMenus.Add(new FlowDefinitionInfoViewModel(flow.Category, flow.Name));
                }
            }
        }

        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> EditConfigCommand { get; }
        public async Task EditConfig(ConfigDefinitionInfoViewModel configDefinitionInfoViewModel)
        {
            //var vm = Navigate<FlowMakerConfigEditViewModel>(HostScreen);
            //await vm.LoadConfig(configDefinitionInfoViewModel.Category, configDefinitionInfoViewModel.Name, configDefinitionInfoViewModel.FlowCategory, configDefinitionInfoViewModel.FlowName);
            //await Task.CompletedTask;
            //_messageBoxManager.Window.Handle(new ModalInfo("牛马配置器", vm, 400) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            //{
            //    LoadConfigMenus();
            //});
        }

        //public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RemoveConfigCommand { get; }
        //public void RemoveConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        //{
        //    _flowProvider.RemoveConfig(
        //        flowDefinitionInfoViewModel.ConfigName,
        //        flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
        //}
        //public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RunCommand { get; }
        //public async Task Run(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        //{
        //    await _flowManager.Run(
        //        flowDefinitionInfoViewModel.ConfigName,
        //        flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name) ;
        //}


        #endregion

    }


}
