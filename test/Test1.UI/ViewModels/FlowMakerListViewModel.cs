using AutoMapper;
using FlowMaker;
using FlowMaker.Models;
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
        private readonly FlowMakerOption _flowMakerOption;

        public FlowMakerListViewModel(IMessageBoxManager messageBoxManager, FlowManager flowManager, IMapper mapper, IServiceProvider serviceProvider, IOptions<FlowMakerOption> options)
        {
            CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
            RemoveCommand = ReactiveCommand.Create<FlowDefinitionInfoViewModel>(Remove);
            ConfigCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Config);
            EditConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(EditConfig);
            RemoveConfigCommand = ReactiveCommand.Create<ConfigDefinitionInfoViewModel>(RemoveConfig);
            RunCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(Run);
            ChangeViewCommand = ReactiveCommand.Create<string>(ChangeView);
            SaveConfigsCommand = ReactiveCommand.Create(SaveConfigs);

            AddTabCommand = ReactiveCommand.CreateFromTask(AddTab);
            DeleteTabCommand = ReactiveCommand.Create(DeleteTab);
            MoveTabCommand = ReactiveCommand.Create<bool>(MoveTab);

            AddBoxCommand = ReactiveCommand.CreateFromTask(AddBoxAsync);
            DeleteBoxCommand = ReactiveCommand.Create(DeleteBox);

            AddActionCommand = ReactiveCommand.CreateFromTask(AddAction);
            DeleteActionCommand = ReactiveCommand.Create(DeleteAction);
            SelectActionCommand = ReactiveCommand.Create<(SpikeBoxViewModel, SpikeActionViewModel)?>(SelectAction);

            MoveActionCommand = ReactiveCommand.Create<bool>(MoveAction);

            AddHeightCommand = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IResizable resizable)
                {
                    return;
                }
                if (resizable is SpikeBoxResizableViewModel)
                {
                    span = 1;
                }

                if (c)
                {
                    resizable.Height += span;
                }
                else
                {
                    if (resizable.Height <= 1)
                    {
                        return;
                    }
                    resizable.Height -= span;
                }
            });
            AddWidthCommand = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IResizable resizable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizableViewModel)
                {
                    span = 1;
                }
                if (c)
                {
                    resizable.Width += span;
                }
                else
                {
                    if (resizable.Width <= 1)
                    {
                        return;
                    }
                    resizable.Width -= span;
                }
            });
            LeftCommand = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IMoveable moveable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizableViewModel)
                {
                    span = 1;
                }
                if (c)
                {
                    moveable.Left += span;
                }
                else
                {
                    if (moveable.Left <= 0)
                    {
                        return;
                    }
                    moveable.Left -= span;
                }
            });
            TopCommand = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IMoveable moveable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizableViewModel)
                {
                    span = 1;
                }
                if (c)
                {
                    moveable.Top += span;
                }
                else
                {
                    if (moveable.Top <= 0)
                    {
                        return;
                    }
                    moveable.Top -= span;
                }
            });
            SelectResizableCommand = ReactiveCommand.Create<IChangeable?>(SelectResizable);
            SelectBoxCommand = ReactiveCommand.Create<SpikeBoxViewModel>(SelectBox);
            ChangeCustomViewCommand = ReactiveCommand.Create<string?>(ChangeCustomView);

            _flowMakerOption = options.Value;
            this._messageBoxManager = messageBoxManager;
            this._flowManager = flowManager;
            _mapper = mapper;
            this._serviceProvider = serviceProvider;
            Edit = _flowMakerOption.Edit;
            InitMenu();
        }

        #region 菜单
        public ObservableCollection<MenuItemViewModel> Menus { get; set; } = [];
        public void InitMenu()
        {
            Menus.Clear();
            Menus.Add(new MenuItemViewModel("主页") { Command = ChangeViewCommand, CommandParameter = "主页" });
            Menus.Add(new MenuItemViewModel("监控") { Command = ChangeViewCommand, CommandParameter = "监控" });
            Menus.Add(new MenuItemViewModel("流程") { Command = ChangeViewCommand, CommandParameter = "流程编辑" });
            Menus.Add(new MenuItemViewModel("配置") { Command = ChangeViewCommand, CommandParameter = "配置编辑" });
            if (ShowMonitor)
            {
                Menus.Add(new MenuItemViewModel("显示全部") { Command = SaveConfigsCommand });
            }
            if (ShowConfigList)
            {
                Menus.Add(new MenuItemViewModel("保存配置") { Command = SaveConfigsCommand });
            }
            if (ShowFlowList)
            {
                Menus.Add(new MenuItemViewModel("创建流程") { Command = CreateCommand });
            }
            if (ShowHome)
            {
                var sections = new MenuItemViewModel("类别");
                Menus.Add(sections);
                foreach (var item in _flowMakerOption.Sections)
                {
                    sections.Children.Add(new MenuItemViewModel(item));
                }
                var editView = new MenuItemViewModel("编辑");
                var tabView = new MenuItemViewModel("标签");
                var bixView = new MenuItemViewModel("盒子");
                var actionView = new MenuItemViewModel("按钮");
                editView.Children.Add(tabView);
                editView.Children.Add(bixView);
                editView.Children.Add(actionView);

                tabView.Children.Add(new MenuItemViewModel("添加标签") { Command = AddTabCommand });
                tabView.Children.Add(new MenuItemViewModel("前移标签") { Command = MoveTabCommand, CommandParameter = false });
                tabView.Children.Add(new MenuItemViewModel("后移标签") { Command = MoveTabCommand, CommandParameter = true });
                tabView.Children.Add(new MenuItemViewModel("删除标签") { Command = DeleteTabCommand });


                bixView.Children.Add(new MenuItemViewModel("添加盒子") { Command = AddBoxCommand });
                if (CurrentBox is not null)
                {
                    bixView.Children.Add(new MenuItemViewModel("删除盒子") { Command = DeleteBoxCommand });
                }

                actionView.Children.Add(new MenuItemViewModel("添加按钮") { Command = AddActionCommand });

                if (CurrentAction is not null)
                {
                    actionView.Children.Add(new MenuItemViewModel("删除按钮") { Command = DeleteActionCommand });
                    actionView.Children.Add(new MenuItemViewModel("前移按钮") { Command = MoveActionCommand, CommandParameter = true });
                    actionView.Children.Add(new MenuItemViewModel("后移按钮") { Command = MoveActionCommand, CommandParameter = false });
                    actionView.Children.Add(new MenuItemViewModel("按钮变化") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.ButtonSize });
                    actionView.Children.Add(new MenuItemViewModel("输入变化") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.InputSize });
                    actionView.Children.Add(new MenuItemViewModel("输出变化") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.OutputSize });
                    actionView.Children.Add(new MenuItemViewModel("边框变化") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.ActionSize });
                }


                var customViews = new MenuItemViewModel("自定义视图");
                foreach (var item in _flowMakerOption.CustomViews)
                {
                    customViews.Children.Add(new MenuItemViewModel("指令") { Command = ChangeCustomViewCommand });
                    customViews.Children.Add(new MenuItemViewModel(item) { Command = ChangeCustomViewCommand, CommandParameter = item });
                }
                editView.Children.Add(customViews);


                Menus.Add(editView);
            }
            foreach (var item in ConfigMenus)
            {
                if (!item.ShowInMenu)
                {
                    continue;
                }
                var menu = Menus.FirstOrDefault(c => c.Name == item.Category);
                if (menu is null)
                {
                    menu = new MenuItemViewModel(item.Category);
                    Menus.Add(menu);
                }
                menu.Children.Add(new MenuItemViewModel(item.Name) { Command = RunCommand, CommandParameter = item });
            }
        }
        [Reactive]
        public bool ShowHome { get; set; } = true;
        [Reactive]
        public bool ShowFlowList { get; set; }
        [Reactive]
        public bool ShowConfigList { get; set; }
        [Reactive]
        public bool ShowMonitor { get; set; }
        public ReactiveCommand<string, Unit> ChangeViewCommand { get; set; }
        public void ChangeView(string viewName)
        {
            switch (viewName)
            {
                case "流程编辑":
                    ShowFlowList = true;
                    ShowConfigList = false;
                    ShowMonitor = false;
                    ShowHome = false;
                    break;
                case "配置编辑":
                    ShowFlowList = false;
                    ShowConfigList = true;
                    ShowMonitor = false;
                    ShowHome = false;
                    break;
                case "监控":
                    ShowFlowList = false;
                    ShowConfigList = false;
                    ShowMonitor = true;
                    ShowHome = false;
                    break;
                case "主页":
                    ShowFlowList = false;
                    ShowConfigList = false;
                    ShowMonitor = false;
                    ShowHome = true;
                    break;
                default:
                    break;
            }
            InitMenu();
        }
        #endregion

        #region 流程与配置
        public ReactiveCommand<FlowDefinitionInfoViewModel?, Unit> CreateCommand { get; }
        public async Task Create(FlowDefinitionInfoViewModel? flowDefinitionInfoViewModel)
        {
            var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
            await vm.Load(flowDefinitionInfoViewModel?.Category, flowDefinitionInfoViewModel?.Name);
            await Task.CompletedTask;
            _messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            {
                LoadFlowMenus();
            });
            //HostScreen.Router.Navigate.Execute(Navigate<FlowMakerEditViewModel>(HostScreen));
        }
        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> RemoveCommand { get; }
        public void Remove(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            _flowManager.RemoveFlow(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
            LoadFlowMenus();
        }
        public ReactiveCommand<FlowDefinitionInfoViewModel, Unit> ConfigCommand { get; }
        public async Task Config(FlowDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            var vm = Navigate<FlowMakerConfigEditViewModel>(HostScreen);
            await vm.Load(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);

            await _messageBoxManager.Modals.Handle(new ModalInfo("配置", vm));
            LoadFlowMenus();
            LoadConfigMenus();
        }

        public override async Task Activate()
        {
            LoadFlowMenus();
            LoadConfigMenus();
            await Task.CompletedTask;
        }

        public ObservableCollection<FlowDefinitionInfoViewModel> FlowMenus { get; set; } = [];
        public void LoadFlowMenus()
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
        public ObservableCollection<ConfigDefinitionInfoViewModel> ConfigMenus { get; set; } = [];
        public void LoadConfigMenus()
        {
            ConfigMenus.Clear();
            var categories = _flowManager.LoadConfigCategories();
            foreach (var category in categories)
            {
                var configs = _flowManager.LoadConfigs(category);
                foreach (var config in configs)
                {
                    ConfigMenus.Add(new ConfigDefinitionInfoViewModel(config.Category, config.Name, config.FlowCategory, config.FlowName, config.CreationTime, config.ModifyTime));
                }
            }
        }
        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> EditConfigCommand { get; }
        public async Task EditConfig(ConfigDefinitionInfoViewModel configDefinitionInfoViewModel)
        {
            var vm = Navigate<FlowMakerConfigEditViewModel>(HostScreen);
            await vm.LoadConfig(configDefinitionInfoViewModel.Category, configDefinitionInfoViewModel.Name, configDefinitionInfoViewModel.FlowCategory, configDefinitionInfoViewModel.FlowName);
            await Task.CompletedTask;
            _messageBoxManager.Window.Handle(new ModalInfo("牛马配置器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            {
                LoadConfigMenus();
            });
        }
        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RemoveConfigCommand { get; }
        public void RemoveConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            _flowManager.RemoveConfig(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name, flowDefinitionInfoViewModel.FlowCategory, flowDefinitionInfoViewModel.FlowName);
            LoadConfigMenus();
        }
        public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RunCommand { get; }
        public async Task Run(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
        {
            await _flowManager.Run(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name, flowDefinitionInfoViewModel.FlowCategory, flowDefinitionInfoViewModel.FlowName);
        }

        public ReactiveCommand<Unit, Unit> SaveConfigsCommand { get; }
        public void SaveConfigs()
        {
            foreach (var item in ConfigMenus)
            {

            }

            InitMenu();
        }
        #endregion

        #region 监控

        #endregion

        #region 主页
        public ICommand AddHeightCommand { get; }
        public ICommand AddWidthCommand { get; }
        public ICommand LeftCommand { get; }
        public ICommand TopCommand { get; }
        public ReactiveCommand<IChangeable?, Unit> SelectResizableCommand { get; }
        public IChangeable? Changeable { get; set; }
        public void SelectResizable(IChangeable? changeableViewModel)
        {
            Changeable = changeableViewModel;
        }
        [Reactive]
        public ObservableCollection<string> SpikeViewModels { get; set; } = [];
        [Reactive]
        public bool Edit { get; set; }

        [Reactive]
        public ObservableCollection<string> Sections { get; set; } = [];

        [Reactive]
        public string? SelectedSection { get; set; }
        /// <summary>
        /// 通过设备类型获取Tabs
        /// </summary>
        /// <param name="deviceType"></param>
        /// <returns></returns>
        public async Task LoadTabs(string deviceType)
        {
            Tabs.Clear();

            var path = Path.Combine(AppContext.BaseDirectory, "Spike");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            path = Path.Combine(path, deviceType + ".json");
            if (!File.Exists(path))
            {
                return;
            }
            var str = await File.ReadAllTextAsync(path);
            if (string.IsNullOrEmpty(str))
            {
                return;
            }
            foreach (var item in JsonSerializer.Deserialize<List<SpikeTab>>(str) ?? [])
            {
                Tabs.Add(_mapper.Map<SpikeTab, SpikeTabViewModel>(item));
            }
            foreach (var tab in Tabs)
            {
                foreach (var box in tab.Boxes)
                {
                    if (!string.IsNullOrEmpty(box.ViewName))
                    {
                        var vm = _serviceProvider.GetKeyedService<ISpikeInjectViewModel>(box.ViewName);
                        if (vm is ISpikeViewModel sVm)
                        {
                            box.DisplayView(box.ViewName, sVm);
                        }
                    }
                    foreach (var action in box.Actions)
                    {
                        foreach (var item in action.Inputs)
                        {
                            item.Value = item.Value;
                        }
                    }
                }
            }
        }
        public async Task Save()
        {
            if (SelectedSection is null)
            {
                return;
            }
            var path = Path.Combine(AppContext.BaseDirectory, "Spike");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            path = Path.Combine(path, SelectedSection + ".json");
            List<SpikeTab> spikeTabs = [];
            foreach (var item in Tabs)
            {
                spikeTabs.Add(_mapper.Map<SpikeTabViewModel, SpikeTab>(item));
            }
            var r = JsonSerializer.Serialize(spikeTabs);
            await File.WriteAllTextAsync(path, r);
        }

        [Reactive]
        public ObservableCollection<SpikeTabViewModel> Tabs { get; set; } = [];

        [Reactive]
        public SpikeTabViewModel? CurrentTab { get; set; }
        public ReactiveCommand<bool, Unit> MoveTabCommand { get; }
        public void MoveTab(bool back)
        {
            if (CurrentTab is null)
            {
                return;
            }
            var old = Tabs.IndexOf(CurrentTab);
            int newIndex;
            if (back)
            {
                newIndex = old + 1;
                if (newIndex >= Tabs.Count)
                {
                    return;
                }
            }
            else
            {
                newIndex = old - 1;
                if (newIndex < 0)
                {
                    return;
                }
            }
            Tabs.Move(old, newIndex);
        }
        public ReactiveCommand<Unit, Unit> AddTabCommand { get; }
        public async Task AddTab()
        {
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入标签名称") { DefaultValue = "New Tab" + (Tabs.Count + 1) });
            if (r.Ok && !string.IsNullOrEmpty(r.Value))
            {
                Tabs.Add(new SpikeTabViewModel() { Name = r.Value });
                if (Tabs.Count == 1)
                {
                    CurrentTab = Tabs.First();
                }
            }

        }

        public ReactiveCommand<Unit, Unit> AddBoxCommand { get; }
        public async Task AddBoxAsync()
        {
            if (CurrentTab is null)
            {
                return;
            }
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入分组名称") { DefaultValue = "New Box" + (CurrentTab.Boxes.Count + 1) });
            if (r.Ok && !string.IsNullOrEmpty(r.Value))
            {
                var left = 0;
                var top = 0;
                if (CurrentTab.Boxes.Any())
                {
                    left = CurrentTab.Boxes.Max(c => c.Size.Left + c.Size.Width);
                    if (left >= 6)
                    {
                        top = CurrentTab.Boxes.Max(c => c.Size.Top + c.Size.Height);
                        left = 0;
                    }
                }

                if (left > 6)
                {
                    left = 6;
                }
                if (top > 6)
                {
                    top = 6;
                }
                var box = new SpikeBoxViewModel() { Name = r.Value };
                box.Size.Left = left;
                box.Size.Top = top;

                CurrentTab.Boxes.Add(box);
            }
        }
        public ReactiveCommand<Unit, Unit> DeleteTabCommand { get; }
        public void DeleteTab()
        {
            if (CurrentTab is null)
            {
                return;
            }
            Tabs.Remove(CurrentTab);
        }
        public SpikeBoxViewModel? CurrentBox { get; set; }
        public ReactiveCommand<SpikeBoxViewModel, Unit> SelectBoxCommand { get; }
        public void SelectBox(SpikeBoxViewModel spikeBoxViewModel)
        {
            if (CurrentAction is not null)
            {
                CurrentAction.Editing = false;
                CurrentAction = null;
            }
            if (CurrentBox is null)
            {
                CurrentBox = spikeBoxViewModel;
                CurrentBox.Editing = true;
                SelectResizable(CurrentBox.Size);
            }
            else
            {
                CurrentBox.Editing = false;
                if (CurrentBox == spikeBoxViewModel)
                {
                    CurrentBox = null;
                    SelectResizable(null);
                    return;
                }
                else
                {
                    CurrentBox = spikeBoxViewModel;
                    CurrentBox.Editing = true;
                }
                SelectResizable(CurrentBox.Size);
            }

        }
        public ReactiveCommand<Unit, Unit> DeleteBoxCommand { get; }
        public void DeleteBox()
        {
            if (CurrentTab is null || CurrentBox is null)
            {
                return;
            }

            CurrentTab.Boxes.Remove(CurrentBox);
        }
        [Reactive]
        public ObservableCollection<string> CustomViews { get; set; } = [];
        public ReactiveCommand<string?, Unit> ChangeCustomViewCommand { get; }
        public void ChangeCustomView(string? viewName)
        {
            if (CurrentBox is null)
            {
                return;
            }
            if (string.IsNullOrEmpty(viewName))
            {
                CurrentBox.DisplayView(viewName, null);
            }
            else
            {
                var vm = _serviceProvider.GetKeyedService<ISpikeInjectViewModel>(viewName);
                if (vm is ISpikeViewModel sVm)
                {
                    CurrentBox.DisplayView(viewName, sVm);
                }
            }
        }


        #region 流程编辑

        public ReactiveCommand<bool, Unit> MoveActionCommand { get; }
        public void MoveAction(bool input)
        {

            if (CurrentAction is null)
            {
                return;
            }
            if (CurrentBox is null)
            {
                return;
            }
            var old = CurrentBox.Actions.IndexOf(CurrentAction);
            int newIndex;
            if (!input)
            {
                newIndex = old + 1;
                if (newIndex >= CurrentBox.Actions.Count)
                {
                    return;
                }
            }
            else
            {
                newIndex = old - 1;
                if (newIndex < 0)
                {
                    return;
                }
            }
            CurrentBox.Actions.Move(old, newIndex);
        }

        public ReactiveCommand<Unit, Unit> AddActionCommand { get; }
        public async Task AddAction()
        {
            if (CurrentBox is null)
            {
                return;
            }
            var vm = Navigate<FlowMakerSelectViewModel>(HostScreen);

            var r = await _messageBoxManager.Modals.Handle(new ModalInfo("选择流程", vm) { OwnerTitle = WindowTitle, Width = 400, Height = 300 });

            if (r)
            {
                if (vm.Definition is not null)
                {
                    CurrentBox.Actions.Add(new SpikeActionViewModel()
                    {
                        DisplayName = vm.DisplayName ?? "BTN",
                        Name = vm.Definition.Name,
                        Category = vm.Definition.Category,
                        Type = vm.Definition.Type,
                    });
                }


            }

        }
        [Reactive]
        public SpikeActionViewModel? CurrentAction { get; set; }
        public ReactiveCommand<(SpikeBoxViewModel, SpikeActionViewModel)?, Unit> SelectActionCommand { get; }
        public void SelectAction((SpikeBoxViewModel, SpikeActionViewModel)? input)
        {
            if (input is null)
            {
                return;
            }
            if (CurrentAction == input.Value.Item2)
            {
                if (CurrentBox is not null)
                {
                    CurrentBox.Editing = false;
                    CurrentBox = null;
                }
                if (CurrentAction is not null)
                {
                    CurrentAction.Editing = false;
                    CurrentAction = null;
                }
            }
            else
            {
                CurrentBox = input.Value.Item1;
                CurrentBox.Editing = false;
                if (CurrentAction is not null)
                {
                    CurrentAction.Editing = false;
                }
                CurrentAction = input.Value.Item2;
                CurrentAction.Editing = true;
            }
            InitMenu();
        }
        public ReactiveCommand<Unit, Unit> DeleteActionCommand { get; }
        public void DeleteAction()
        {
            if (CurrentAction is null)
            {
                return;
            }
            if (CurrentBox is null)
            {
                return;
            }
            CurrentBox.Actions.Remove(CurrentAction);
        }
        #endregion
        #endregion
    }

    #region 主页
    public class SpikeTab
    {
        public required string Name { get; set; }

        public List<SpikeBox> Boxes { get; set; } = [];

    }
    public class SpikeBox
    {
        public required string Name { get; set; }

        public string? ViewName { get; set; }

        public SpikeMoveAndResizable Size { get; set; } = new SpikeMoveAndResizable();


        public List<SpikeAction> Actions { get; set; } = [];

    }
    public class SpikeAction
    {
        public required string Name { get; set; }

        public SpikeResizable ActionSize { get; set; } = new SpikeResizable();


        public SpikeMoveAndResizable ButtonSize { get; set; } = new SpikeMoveAndResizable();


        public SpikeMoveAndResizable InputSize { get; set; } = new SpikeMoveAndResizable();


        public SpikeMoveAndResizable OutputSize { get; set; } = new SpikeMoveAndResizable();


        public required string DeviceType { get; set; }

        public required string GroupName { get; set; }

        public string? WorkflowName { get; set; }

        public List<FlowInput> Inputs { get; set; } = [];


        public List<FlowOutput> Outputs { get; set; } = [];

    }
    public class SpikeMoveable : IMoveable, IChangeable
    {
        public int Left { get; set; }

        public int Top { get; set; }
    }
    public class SpikeResizable : IResizable, IChangeable
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }
    public class SpikeMoveAndResizable : IResizable, IChangeable, IMoveable
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Left { get; set; }

        public int Top { get; set; }
    }
    public class SpikeTabViewModel : ReactiveObject
    {
        [Reactive]
        public required string Name { get; set; }
        [Reactive]
        public ObservableCollection<SpikeBoxViewModel> Boxes { get; set; } = [];
    }
    public class SpikeBoxViewModel : ReactiveObject, IScreen
    {
        private bool showView;

        [Reactive]
        public bool Editing { get; set; }
        public required string Name { get; set; }
        [Reactive]
        public string? ViewName { get; set; }
        [Reactive]
        public ISpikeViewModel? SpikeViewModel { get; set; }
        public bool ShowView
        {
            get => showView; set
            {
                this.RaiseAndSetIfChanged(ref showView, value);
                if (!value)
                {
                    ViewName = null;
                    Router.NavigationStack.Clear();
                }
            }
        }

        [Reactive]
        public SpikeBoxResizableViewModel Size { get; set; } = new();

        [Reactive]
        public ObservableCollection<SpikeActionViewModel> Actions { get; set; } = [];

        [Reactive]
        public RoutingState Router { get; set; } = new RoutingState();

        public void DisplayView(string? viewName, ISpikeViewModel? spikeViewModel)
        {
            ViewName = viewName;
            SpikeViewModel = spikeViewModel;
            if (SpikeViewModel is ViewModelBase modelBase)
            {
                ShowView = true;
                modelBase.SetScreen(this);
                Router.Navigate.Execute(modelBase);
            }
            else
            {
                ShowView = false;
            }
        }
    }
    public class SpikeActionViewModel : ReactiveObject
    {
        [Reactive]
        public required string DisplayName { get; set; }
        [Reactive]
        public required string Category { get; set; }
        [Reactive]
        public required string Name { get; set; }
        [Reactive]
        public DefinitionType Type { get; set; }
        [Reactive]
        public bool Editing { get; set; }
        [Reactive]
        public SpikeResizableViewModel ActionSize { get; set; } = new();
        [Reactive]
        public SpikeMoveAndResizableViewModel ButtonSize { get; set; } = new()
        {
            Top = 30,
            Left = 60,
            Width = 80,
            Height = 30,
        };
        [Reactive]
        public SpikeMoveAndResizableViewModel InputSize { get; set; } = new()
        {
            Top = 30,
            Left = 0,
            Width = 60,
            Height = 60,
        };
        [Reactive]
        public SpikeMoveAndResizableViewModel OutputSize { get; set; } = new()
        {
            Top = 30,
            Left = 150,
            Width = 60,
            Height = 60,
        };



        public SpikeActionViewModel()
        {
            ActionSize.Width = 200;
            ActionSize.Height = 100;
        }

        [Reactive]
        public ObservableCollection<SpikeInputViewModel> Inputs { get; set; } = [];
        [Reactive]
        public ObservableCollection<SpikeOutputViewModel> Outputs { get; set; } = [];
    }
    public class SpikeOutputViewModel : ReactiveObject
    {
        [Reactive]
        public required string DisplayName { get; set; }
        [Reactive]
        public string? Value { get; set; }
    }
    public class SpikeInputViewModel : ReactiveObject
    {
        [Reactive]
        public required string DisplayName { get; set; }
        [Reactive]
        public required string Type { get; set; }
        [Reactive]
        public string? Value { get; set; }
        [Reactive]
        public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];
    }


    public class SpikeResizableViewModel : ReactiveObject, IResizable
    {
        [Reactive]
        public int Height { get; set; } = 100;
        [Reactive]
        public int Width { get; set; } = 100;

    }
    public class SpikeMoveableViewModel : ReactiveObject, IMoveable
    {
        [Reactive]
        public int Left { get; set; } = 50;
        [Reactive]
        public int Top { get; set; } = 50;

    }
    public class SpikeMoveAndResizableViewModel : ReactiveObject, IResizable, IMoveable
    {
        [Reactive]
        public int Left { get; set; }
        [Reactive]
        public int Top { get; set; }
        [Reactive]
        public int Height { get; set; } = 2;
        [Reactive]
        public int Width { get; set; } = 2;

    }
    public class SpikeBoxResizableViewModel : ReactiveObject, IResizable, IMoveable
    {
        [Reactive]
        public int Left { get; set; }
        [Reactive]
        public int Top { get; set; }
        [Reactive]
        public int Height { get; set; } = 2;
        [Reactive]
        public int Width { get; set; } = 2;

    }

    public class SpikeActionStatusViewModel : ReactiveObject
    {
        [Reactive]
        public required string Id { get; set; }
        [Reactive]
        public required string WorkflowName { get; set; }
        [Reactive]
        public required string Status { get; set; }
    }
    public interface IResizable : IChangeable
    {
        int Width { get; set; }

        int Height { get; set; }
    }
    public interface IMoveable : IChangeable
    {
        int Left { get; set; }

        int Top { get; set; }
    }

    public interface IChangeable
    {
    }

    public interface ISpikeViewModel : ISpikeInjectViewModel, IRoutableViewModel
    {
        static abstract string ViewName { get; }
    }
    public interface ISpikeInjectViewModel
    {
    }
    #endregion

    public class MenuConfig
    {
        public string Category { get; set; }

        public string Name { get; set; }
        public bool ShowInMenu { get; set; }
    }

    public class MenuItemViewModel(string name) : ReactiveObject
    {
        [Reactive]
        public string Name { get; set; } = name;

        [Reactive]
        public ICommand? Command { get; set; }
        [Reactive]
        public object? CommandParameter { get; set; }
        public ObservableCollection<MenuItemViewModel> Children { get; set; } = [];
    }

    public class FlowDefinitionInfoViewModel(string category, string name, DateTime creationTime, DateTime modifyTime) : ReactiveObject
    {
        [Reactive]
        public string Category { get; set; } = category;

        [Reactive]
        public string Name { get; set; } = name;
        [Reactive]
        public DateTime CreationTime { get; set; } = creationTime;
        [Reactive]
        public DateTime ModifyTime { get; set; } = modifyTime;
    }
    public class ConfigDefinitionInfoViewModel(string category, string name, string flowCategory, string flowName, DateTime creationTime, DateTime modifyTime) : FlowDefinitionInfoViewModel(category, name, creationTime, modifyTime)
    {
        [Reactive]
        public string FlowCategory { get; set; } = flowCategory;

        [Reactive]
        public string FlowName { get; set; } = flowName;

        [Reactive]
        public bool ShowInMenu { get; set; }
    }
}
