using DynamicData;
using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Xml.Linq;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels
{
    public class FlowMakerMainViewModel : ViewModelBase, IScreen
    {
        public RoutingState Router { get; } = new RoutingState();
        private readonly FlowMakerOption _flowMakerOption;
        private readonly IServiceProvider _serviceProvider;
        private readonly FlowManager _flowManager;
        private readonly IMessageBoxManager _messageBoxManager;
        private readonly IFlowProvider _flowProvider;

        public FlowMakerMainViewModel(IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, FlowManager flowManager, IMessageBoxManager messageBoxManager, IFlowProvider flowProvider)
        {
            _flowMakerOption = options.Value;
            _serviceProvider = serviceProvider;
            _flowManager = flowManager;
            this._messageBoxManager = messageBoxManager;
            this._flowProvider = flowProvider;
            ChangeViewCommand = ReactiveCommand.Create(ChangeView);
            ShowLogCommand = ReactiveCommand.CreateFromTask<MonitorRunningViewModel>(ShowLog);
            ReloadMenuCommand = ReactiveCommand.Create<IList<MenuItemViewModel>>(c =>
            {
                Menus.Clear();
                Menus.Add(c);
            });

            CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
            RemoveCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Remove);
            ExecuteFlowCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(ExecuteFlow);
            RemoveConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RemoveConfig);
            RunConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RunConfig);
            LoadConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(LoadConfig);
        }
        public ObservableCollection<MenuItemViewModel> Menus { get; set; } = [];

        public CompositeDisposable Disposables { get; set; } = [];

        public ObservableCollection<MonitorRunningViewModel> Runnings { get; set; } = [];

        public override async Task Activate()
        {
            ChangeView();

            await LoadFlows();

            MessageBus.Current.Listen<MonitorMessage>().Subscribe(c =>
            {
                var id = c.Context.FlowIds[0];
                var running = Runnings.FirstOrDefault(v => v.Id == c.Context.FlowIds[0]);
                if (running is null)
                {
                    running = new MonitorRunningViewModel() { DisplayName = c.Context.FlowDefinition.Category + ":" + c.Context.FlowDefinition.Name, RunnerState = c.RunnerState, Id = c.Context.FlowIds[0], TotalCount = c.TotalCount };
                    Runnings.Insert(0, running);
                    var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                    if (mid is MonitorMiddleware monitor)
                    {
                        running.StepChange = monitor.PercentChange.Subscribe(c =>
                        {
                            running.Percent = c;
                        });
                    }
                }
                else
                {
                    running.RunnerState = c.RunnerState;
                }
            }).DisposeWith(Disposables);

        }
        public int MyProperty { get; set; }
        [Reactive]
        public string PageName { get; set; } = "测试";
        public ReactiveCommand<Unit, Unit> ChangeViewCommand { get; set; }
        public async void ChangeView()
        {
            Menus.Clear();
            switch (PageName)
            {
                case "测试":
                    {
                        var vm = _serviceProvider.GetRequiredService<FlowMakerMonitorViewModel>();
                        vm.SetScreen(this);
                        Menus.Add(vm.InitMenu());

                        await Router.NavigateAndReset.Execute(vm);
                        PageName = "自定义";
                    }
                    break;
                default:
                    {
                        if (string.IsNullOrEmpty(_flowMakerOption.Section))
                        {
                            return;
                        }
                        var vm = _serviceProvider.GetRequiredService<FlowMakerCustomPageViewModel>();
                        vm.SetScreen(this);
                        await Router.NavigateAndReset.Execute(vm);
                        await vm.LoadTabs();
                        vm.ReloadMenuCommand = ReloadMenuCommand;
                        Menus.Add(vm.InitMenu());
                        PageName = "测试";
                    }
                    break;
            }
        }
        public ReactiveCommand<IList<MenuItemViewModel>, Unit> ReloadMenuCommand { get; }

        public ReactiveCommand<MonitorRunningViewModel, Unit> ShowLogCommand { get; }
        public async Task ShowLog(MonitorRunningViewModel monitorRunningViewModel)
        {
            var vm = _serviceProvider.GetRequiredService<FlowMakerLogViewModel>();
            await vm.Load(monitorRunningViewModel.Id);
            _messageBoxManager.Window.Handle(new ModalInfo("牛马日志", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c => { });
        }


        #region FlowTree

        public ObservableCollection<FlowCategoryViewModel> Categories { get; set; } = [];
        public Task LoadFlows()
        {
            Categories.Clear();
            _flowProvider.LoadCategories().ToList().ForEach(c =>
            {
                var category = new FlowCategoryViewModel(c);
                Categories.Add(category);
                _flowProvider.LoadFlows(c).ToList().ForEach(c =>
                {
                    var flow = new FlowDefinitionInfoViewModel(category.Category, c.Name);
                    category.Flows.Add(flow);
                    foreach (var item in c.Configs)
                    {
                        flow.Configs.Add(new ConfigDefinitionInfoViewModel(c.Category, c.Name, item));
                    }
                });
            });

            return Task.CompletedTask;

        }

        public ReactiveCommand<FlowDefinitionInfoViewModel?, Unit> CreateCommand { get; }
        public async Task Create(FlowDefinitionInfoViewModel? flowDefinitionInfoViewModel)
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            await vm.Load(flowDefinitionInfoViewModel?.Category, flowDefinitionInfoViewModel?.Name);
            var title = "牛马编辑器" + " " + flowDefinitionInfoViewModel?.Category + " " + flowDefinitionInfoViewModel?.Name;
            _messageBoxManager.Window.Handle(new ModalInfo(title, vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            {
                LoadFlows();
            });
        }
        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> RemoveCommand { get; }
        public async Task Remove(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            await _flowProvider.RemoveFlow(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
            await LoadFlows();
        }

        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> ExecuteFlowCommand { get; }
        public async Task ExecuteFlow(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            var vm = _serviceProvider.GetRequiredService<FlowMakerDebugViewModel>();
            vm.FlowCategory = flowDefinitionInfoViewModel.Category;
            vm.FlowName = flowDefinitionInfoViewModel.Name;
            vm.ConfigName = null;
            await vm.Load();
            MessageBus.Current.SendMessage(vm, "AddDebug");
        }




        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> LoadConfigCommand { get; set; }
        public async Task LoadConfig(ConfigDefinitionInfoViewModel model)
        {
            var vm = _serviceProvider.GetRequiredService<FlowMakerDebugViewModel>();
            vm.FlowCategory = model.Category;
            vm.FlowName = model.Name;
            vm.ConfigName = model.ConfigName;
            await vm.Load();
            MessageBus.Current.SendMessage(vm, "AddDebug");
        }

        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RemoveConfigCommand { get; }
        public async Task RemoveConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            await _flowProvider.RemoveConfig(
                   flowDefinitionInfoViewModel.ConfigName,
                   flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
            await LoadFlows();
        }
        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RunConfigCommand { get; }
        public async Task RunConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            await _flowManager.Run(
                flowDefinitionInfoViewModel.ConfigName,
                flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
        }


        #endregion
    }
}
