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
            DeleteActionCommand = ReactiveCommand.Create<(SpikeBoxViewModel, SpikeActionViewModel)?>(DeleteAction);
            //AddActionCommand = ReactiveCommand.CreateFromTask<(SpikeTabViewModel, SpikeBoxViewModel)?>(AddAction);
            //EditActionCommand = ReactiveCommand.CreateFromTask<SpikeActionViewModel>(EditAction);
            MoveForwardActionCommand = ReactiveCommand.Create<(SpikeBoxViewModel, SpikeActionViewModel)?>(c =>
            {
                if (c is null)
                {
                    return;
                }
                MoveAction((c.Value.Item1, c.Value.Item2, true));
            });
            MoveBackActionCommand = ReactiveCommand.Create<(SpikeBoxViewModel, SpikeActionViewModel)?>(c =>
            {
                if (c is null)
                {
                    return;
                }
                MoveAction((c.Value.Item1, c.Value.Item2, false));
            });

            AddHeightCommnd = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IResizeable resizeable)
                {
                    return;
                }
                if (resizeable is SpikeBoxResizeableViewModel)
                {
                    span = 1;
                }

                if (c)
                {
                    resizeable.Height += span;
                }
                else
                {
                    if (resizeable.Height <= 1)
                    {
                        return;
                    }
                    resizeable.Height -= span;
                }
            });
            AddWidthCommnd = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IResizeable resizeable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizeableViewModel)
                {
                    span = 1;
                }
                if (c)
                {
                    resizeable.Width += span;
                }
                else
                {
                    if (resizeable.Width <= 1)
                    {
                        return;
                    }
                    resizeable.Width -= span;
                }
            });
            LeftCommnd = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IMoveable moveable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizeableViewModel)
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
            TopCommnd = ReactiveCommand.Create<bool>(c =>
            {
                int span = 5;
                if (Changeable is null || Changeable is not IMoveable moveable)
                {
                    return;
                }
                if (Changeable is SpikeBoxResizeableViewModel)
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
            SelectResizeableCommand = ReactiveCommand.Create<IChangeable?>(SelectResizeable);
            SelectBoxCommand = ReactiveCommand.Create<SpikeBoxViewModel>(SelectBox);
            ChangeCustomViewCommand = ReactiveCommand.Create<string?>(ChangeCustomView);

            _flowMakerOption = options.Value;
            this._messageBoxManager = messageBoxManager;
            this._flowManager = flowManager;
            _mapper = mapper;
            this._serviceProvider = serviceProvider;
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
                var editview = new MenuItemViewModel("编辑");
                editview.Children.Add(new MenuItemViewModel("添加标签") { Command = AddTabCommand });
                editview.Children.Add(new MenuItemViewModel("添加盒子") { Command = AddBoxCommand });
                editview.Children.Add(new MenuItemViewModel("添加按钮") { Command = AddActionCommand });
                var customViews = new MenuItemViewModel("自定义视图");
                foreach (var item in _flowMakerOption.CustomViews)
                {
                    customViews.Children.Add(new MenuItemViewModel("指令") { Command = ChangeCustomViewCommand });
                    customViews.Children.Add(new MenuItemViewModel(item) { Command = ChangeCustomViewCommand, CommandParameter = item });
                }
                editview.Children.Add(customViews);

                editview.Children.Add(new MenuItemViewModel("前移标签") { Command = MoveTabCommand, CommandParameter = false });
                editview.Children.Add(new MenuItemViewModel("后移标签") { Command = MoveTabCommand, CommandParameter = true });
                editview.Children.Add(new MenuItemViewModel("删除标签") { Command = DeleteTabCommand });
                editview.Children.Add(new MenuItemViewModel("删除盒子") { Command = DeleteBoxCommand });
                Menus.Add(editview);
            }
            Edit = true;
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
        public ICommand EditCommand { get; }
        public ICommand AddHeightCommnd { get; }
        public ICommand AddWidthCommnd { get; }
        public ICommand LeftCommnd { get; }
        public ICommand TopCommnd { get; }
        public ReactiveCommand<IChangeable?, Unit> SelectResizeableCommand { get; }
        public ICommand TerminateCommand { get; }
        public IChangeable? Changeable { get; set; }
        public void SelectResizeable(IChangeable? changeableViewModel)
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
                foreach (var box in tab.Boxs)
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
            int newindex;
            if (back)
            {
                newindex = old + 1;
                if (newindex >= Tabs.Count)
                {
                    return;
                }
            }
            else
            {
                newindex = old - 1;
                if (newindex < 0)
                {
                    return;
                }
            }
            Tabs.Move(old, newindex);
        }
        public ReactiveCommand<Unit, Unit> AddTabCommand { get; }
        public async Task AddTab()
        {
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入标签名称") { DefautValue = "New Tab" + (Tabs.Count + 1) });
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
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入分组名称") { DefautValue = "New Box" + (CurrentTab.Boxs.Count + 1) });
            if (r.Ok && !string.IsNullOrEmpty(r.Value))
            {
                var left = 1;
                var top = 1;
                if (CurrentTab.Boxs.Any())
                {
                    left = CurrentTab.Boxs.Max(c => c.Size.Left + c.Size.Width);
                    top = CurrentTab.Boxs.Max(c => c.Size.Top + c.Size.Height);
                }

                if (left > 8)
                {
                    left = 8;
                }
                if (top > 8)
                {
                    top = 8;
                }
                var box = new SpikeBoxViewModel() { Name = r.Value };
                box.Size.Left = left;
                box.Size.Top = top;

                CurrentTab.Boxs.Add(box);
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
            if (CurrentBox is null)
            {
                CurrentBox = spikeBoxViewModel;
                CurrentBox.Editing = true;
                SelectResizeable(CurrentBox.Size);
            }
            else
            {
                CurrentBox.Editing = false;
                if (CurrentBox == spikeBoxViewModel)
                {
                    CurrentBox = null;
                    SelectResizeable(null);
                    return;
                }
                else
                {
                    CurrentBox = spikeBoxViewModel;
                    CurrentBox.Editing = true;
                }
                SelectResizeable(CurrentBox.Size);
            }

        }
        public ReactiveCommand<Unit, Unit> DeleteBoxCommand { get; }
        public void DeleteBox()
        {
            if (CurrentTab is null || CurrentBox is null)
            {
                return;
            }

            CurrentTab.Boxs.Remove(CurrentBox);
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

        public ReactiveCommand<(SpikeBoxViewModel, SpikeActionViewModel)?, Unit> MoveForwardActionCommand { get; }
        public ReactiveCommand<(SpikeBoxViewModel, SpikeActionViewModel)?, Unit> MoveBackActionCommand { get; }
        public void MoveAction((SpikeBoxViewModel, SpikeActionViewModel, bool) input)
        {
            var old = input.Item1.Actions.IndexOf(input.Item2);
            int newindex;
            if (!input.Item3)
            {
                newindex = old + 1;
                if (newindex >= input.Item1.Actions.Count)
                {
                    return;
                }
            }
            else
            {
                newindex = old - 1;
                if (newindex < 0)
                {
                    return;
                }
            }
            input.Item1.Actions.Move(old, newindex);
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
        public ReactiveCommand<(SpikeBoxViewModel, SpikeActionViewModel)?, Unit> DeleteActionCommand { get; }
        public void DeleteAction((SpikeBoxViewModel, SpikeActionViewModel)? input)
        {
            if (input is null)
            {
                return;
            }
            input.Value.Item1.Actions.Remove(input.Value.Item2);
        }
        #endregion
        #endregion
    }

    #region 主页
    public class SpikeTab
    {
        public required string Name { get; set; }

        public List<SpikeBox> Boxs { get; set; } = new List<SpikeBox>();

    }
    public class SpikeBox
    {
        public required string Name { get; set; }

        public string? ViewName { get; set; }

        public SpikeMoveAndResizeable Size { get; set; } = new SpikeMoveAndResizeable();


        public List<SpikeAction> Actions { get; set; } = [];

    }
    public class SpikeAction
    {
        public required string Name { get; set; }

        public SpikeResizeable AcitonSize { get; set; } = new SpikeResizeable();


        public SpikeMoveAndResizeable ButtonSize { get; set; } = new SpikeMoveAndResizeable();


        public SpikeMoveAndResizeable InputSize { get; set; } = new SpikeMoveAndResizeable();


        public SpikeMoveAndResizeable OutputSize { get; set; } = new SpikeMoveAndResizeable();


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
    public class SpikeResizeable : IResizeable, IChangeable
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }
    public class SpikeMoveAndResizeable : IResizeable, IChangeable, IMoveable
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
        public ObservableCollection<SpikeBoxViewModel> Boxs { get; set; } = [];
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
        public SpikeBoxResizeableViewModel Size { get; set; } = new();

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
        public SpikeResizeableViewModel AcitonSize { get; set; } = new();
        [Reactive]
        public SpikeMoveAndResizeableViewModel ButtonSize { get; set; } = new()
        {
            Top = 30,
            Left = 60,
            Width = 80,
            Height = 30,
        };
        [Reactive]
        public SpikeMoveAndResizeableViewModel InputSize { get; set; } = new()
        {
            Top = 30,
            Left = 0,
            Width = 60,
            Height = 60,
        };
        [Reactive]
        public SpikeMoveAndResizeableViewModel OutputSize { get; set; } = new()
        {
            Top = 30,
            Left = 150,
            Width = 60,
            Height = 60,
        };



        public SpikeActionViewModel()
        {
            AcitonSize.Width = 200;
            AcitonSize.Height = 100;
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


    public class SpikeResizeableViewModel : ReactiveObject, IResizeable
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
    public class SpikeMoveAndResizeableViewModel : ReactiveObject, IResizeable, IMoveable
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
    public class SpikeBoxResizeableViewModel : ReactiveObject, IResizeable, IMoveable
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
    public interface IResizeable : IChangeable
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
