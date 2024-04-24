using AutoMapper;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels
{
    public class FlowMakerCustomPageViewModel : ViewModelBase
    {
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBoxManager _messageBoxManager;
        private readonly FlowManager _flowManager;
        private readonly IFlowProvider _flowProvider;
        private readonly FlowMakerOption _flowMakerOption;

        public FlowMakerCustomPageViewModel(IMapper mapper, IServiceProvider serviceProvider, IMessageBoxManager messageBoxManager, IOptions<FlowMakerOption> options, FlowManager flowManager, IFlowProvider flowProvider)
        {
            AddTabCommand = ReactiveCommand.CreateFromTask(AddTab);
            DeleteTabCommand = ReactiveCommand.Create(DeleteTab);
            MoveTabCommand = ReactiveCommand.Create<bool>(MoveTab);

            AddBoxCommand = ReactiveCommand.CreateFromTask(AddBoxAsync);
            DeleteBoxCommand = ReactiveCommand.Create(DeleteBox);
            ChangeBoxShowViewCommand = ReactiveCommand.Create(ChangeBoxShowView);
            ChangeBoxCustomViewGroupCommand = ReactiveCommand.Create<string>(ChangeBoxCustomViewGroup);
            ChangeBoxCustomViewNameCommand = ReactiveCommand.CreateFromTask<string>(ChangeBoxCustomViewNameAsync);

            AddActionCommand = ReactiveCommand.CreateFromTask(AddAction);
            DeleteActionCommand = ReactiveCommand.Create(DeleteAction);
            SelectActionCommand = ReactiveCommand.Create<(SpikeBoxViewModel, SpikeActionViewModel)?>(SelectAction);

            MoveActionCommand = ReactiveCommand.Create<bool>(MoveAction);

            AddHeightCommand = ReactiveCommand.Create<bool>(AddHeight);
            AddWidthCommand = ReactiveCommand.Create<bool>(AddWidth);
            LeftCommand = ReactiveCommand.Create<bool>(Left);
            TopCommand = ReactiveCommand.Create<bool>(Top);
            SelectResizableCommand = ReactiveCommand.Create<IChangeable?>(SelectResizable);
            SelectBoxCommand = ReactiveCommand.Create<SpikeBoxViewModel>(SelectBox);
            ChangeCustomViewCommand = ReactiveCommand.Create<string?>(ChangeCustomView);
            SaveCommand = ReactiveCommand.CreateFromTask(Save);
            ChangeEditCommand = ReactiveCommand.Create(ChangeEdit);
            LoadTabsCommand = ReactiveCommand.CreateFromTask(LoadTabs);
            RunCommand = ReactiveCommand.CreateFromTask<SpikeActionViewModel>(Run);

            this.WhenAnyValue(c => c.CurrentTab).Subscribe(c =>
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
                Reload();
            });

            _mapper = mapper;
            _serviceProvider = serviceProvider;
            _messageBoxManager = messageBoxManager;
            _flowManager = flowManager;
            _flowProvider = flowProvider;
            _flowMakerOption = options.Value;
            CanEdit = _flowMakerOption.Edit;
            InitMenu();
        }
        [Reactive]
        public bool CanEdit { get; set; }
        [Reactive]
        public ReactiveCommand<IList<MenuItemViewModel>, Unit>? ReloadMenuCommand { get; set; }
        #region 菜单
        public IList<MenuItemViewModel> InitMenu(string? group = null)
        {
            List<MenuItemViewModel> menus = [];
            if (!Edit)
            {
                if (CanEdit)
                {
                    menus.Add(new MenuItemViewModel("编辑") { Command = ChangeEditCommand });
                }

                return menus;
            }

            menus.Add(new MenuItemViewModel("保存") { Command = SaveCommand });


            if (CurrentAction is not null)
            {
                menus.Add(new MenuItemViewModel("删除按钮") { Command = DeleteActionCommand });
                menus.Add(new MenuItemViewModel("前移按钮") { Command = MoveActionCommand, CommandParameter = true });
                menus.Add(new MenuItemViewModel("后移按钮") { Command = MoveActionCommand, CommandParameter = false });
                menus.Add(new MenuItemViewModel("边框变化(z)") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.ActionSize });
                menus.Add(new MenuItemViewModel("按钮变化(x)") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.ButtonSize });
                menus.Add(new MenuItemViewModel("输入变化(c)") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.InputSize });
                menus.Add(new MenuItemViewModel("输出变化(v)") { Command = SelectResizableCommand, CommandParameter = CurrentAction?.OutputSize });
            }
            else if (CurrentBox is not null)
            {
                menus.Add(new MenuItemViewModel("删除盒子") { Command = DeleteBoxCommand });
                if (CurrentBox.ShowView)
                {
                    menus.Add(new MenuItemViewModel("流程盒子") { Command = ChangeBoxShowViewCommand });
                    if (string.IsNullOrEmpty(group))
                    {
                        foreach (var item in _flowMakerOption.Group.Where(v => v.Value.CustomPageViewDefinitions.Count != 0))
                        {
                            menus.Add(new MenuItemViewModel(item.Key) { Command = ChangeBoxCustomViewGroupCommand, CommandParameter = item.Key });
                        }
                    }
                    else
                    {
                        foreach (var item in _flowMakerOption.Group[group].CustomPageViewDefinitions)
                        {
                            menus.Add(new MenuItemViewModel(group + ":" + item.Name) { Command = ChangeBoxCustomViewNameCommand, CommandParameter = item.Name });
                        }
                    }
                }
                else
                {
                    menus.Add(new MenuItemViewModel("自定义盒子") { Command = ChangeBoxShowViewCommand });
                    menus.Add(new MenuItemViewModel("添加按钮") { Command = AddActionCommand });
                }
            }
            else
            {
                menus.Add(new MenuItemViewModel("添加标签") { Command = AddTabCommand });
                menus.Add(new MenuItemViewModel("添加盒子") { Command = AddBoxCommand });
                menus.Add(new MenuItemViewModel("前移标签") { Command = MoveTabCommand, CommandParameter = false });
                menus.Add(new MenuItemViewModel("后移标签") { Command = MoveTabCommand, CommandParameter = true });
                menus.Add(new MenuItemViewModel("删除标签") { Command = DeleteTabCommand });
            }

            return menus;
        }
        #endregion
        #region 主页
        public ReactiveCommand<Unit, Unit> ChangeEditCommand { get; }
        public void ChangeEdit()
        {
            Edit = !Edit;
            if (!Edit)
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

            Reload();
        }

        public ReactiveCommand<Unit, Unit> ChangeBoxShowViewCommand { get; }
        public void ChangeBoxShowView()
        {
            if (CurrentBox is not null)
            {
                CurrentBox.ShowView = !CurrentBox.ShowView;
                if (CurrentBox.ShowView)
                {
                    CurrentBox.Actions.Clear();
                }
                else
                {
                    CurrentBox.Inputs.Clear();
                    CurrentBox.ViewGroup = null;
                    CurrentBox.ViewName = null;
                }
            }
            Reload();
        }
        public ReactiveCommand<string, Unit> ChangeBoxCustomViewGroupCommand { get; }
        public void ChangeBoxCustomViewGroup(string group)
        {
            if (CurrentBox is not null)
            {
                CurrentBox.ViewGroup = group;
            }
            Reload(group);
        }
        public ReactiveCommand<string, Unit> ChangeBoxCustomViewNameCommand { get; }
        public async Task ChangeBoxCustomViewNameAsync(string name)
        {
            if (CurrentBox is null || string.IsNullOrEmpty(CurrentBox.ViewGroup))
            {
                return;
            }
            CurrentBox.Router.NavigationStack.Clear();
            CurrentBox.ViewName = name;
            CurrentBox.Inputs.Clear();
            foreach (var item in _flowMakerOption.Group[CurrentBox.ViewGroup].CustomPageViewDefinitions.First(c => c.Name == name).Data)
            {
                var input = new SpikeBoxCustomViewInputViewModel() { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = item.DefaultValue };
                if (!string.IsNullOrEmpty(item.OptionProviderName))
                {
                    var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(item.OptionProviderName);
                    if (pp is null)
                    {
                        return;
                    }
                    var options = await pp.GetOptions();

                    input.Options.Clear();
                    foreach (var option in options)
                    {
                        input.Options.Add(new OptionDefinition(option.Name, option.Value));
                    }
                }
                else
                {
                    foreach (var option in item.Options)
                    {
                        input.Options.Add(new OptionDefinition(option.DisplayName, option.Name));
                    }
                }
                input.HasOption = input.Options.Count > 0;
                CurrentBox.Inputs.Add(input);
            }

            Reload();
        }


        public void Reload(string? group = null)
        {
            if (ReloadMenuCommand is not null)
            {
                var list = InitMenu(group);
                ReloadMenuCommand.Execute(list).Subscribe();
            }
        }
        public ReactiveCommand<bool, Unit> AddHeightCommand { get; }
        public void AddHeight(bool c)
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
        }
        public ReactiveCommand<bool, Unit> AddWidthCommand { get; }
        public void AddWidth(bool c)
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
        }
        public ReactiveCommand<bool, Unit> LeftCommand { get; }
        public void Left(bool c)
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
        }
        public ReactiveCommand<bool, Unit> TopCommand { get; }
        public void Top(bool c)
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
        }
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


        public ReactiveCommand<Unit, Unit> LoadTabsCommand { get; set; }
        /// <summary>
        /// 通过设备类型获取Tabs
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public async Task LoadTabs()
        {
            Tabs.Clear();

            if (!Directory.Exists(_flowMakerOption.CustomPageRootDir))
            {
                Directory.CreateDirectory(_flowMakerOption.CustomPageRootDir);
            }
            var path = Path.Combine(_flowMakerOption.CustomPageRootDir, _flowMakerOption.Section + ".json");
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
            if (Tabs.Count > 0)
            {
                CurrentTab = Tabs[0];
            }
            foreach (var tab in Tabs)
            {
                foreach (var box in tab.Boxes)
                {
                    if (!string.IsNullOrEmpty(box.ViewName) && !string.IsNullOrEmpty(box.ViewGroup))
                    {
                        var vm = _serviceProvider.GetKeyedService<ICustomPageInjectViewModel>(box.ViewGroup + ":" + box.ViewName);
                        if (vm is ICustomPageViewModel sVm)
                        {
                            await sVm.WrapAsync(box.Inputs.Select(c => new FlowInput() { Id = Guid.NewGuid(), Name = c.Name, Mode = InputMode.Normal, Value = c.Value }).ToList(), _serviceProvider, CancellationToken.None);
                            box.DisplayView(sVm);
                        }
                    }
                    foreach (var action in box.Actions)
                    {
                        if (string.IsNullOrEmpty(action.ConfigName))
                        {
                            var def = await _flowProvider.LoadFlowDefinitionAsync(action.Category, action.Name);
                            foreach (var item in def.Data)
                            {
                                if (item.IsInput)
                                {
                                    var data = new SpikeInputViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
                                    if (!string.IsNullOrWhiteSpace(item.OptionProviderName))
                                    {
                                        var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(item.Type + ":" + item.OptionProviderName);
                                        if (pp is not null)
                                        {
                                            var options = await pp.GetOptions();
                                            foreach (var option in options)
                                            {
                                                data.Options.Add(new FlowStepOptionViewModel(option.Value, option.Name));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (var option in item.Options)
                                        {
                                            data.Options.Add(new FlowStepOptionViewModel(option.Name, option.DisplayName));
                                        }
                                    }

                                    if (data.Options.Count != 0)
                                    {
                                        data.HasOption = true;
                                    }
                                    action.Inputs.Add(data);
                                }
                                if (item.IsOutput)
                                {
                                    action.Outputs.Add(new SpikeOutputViewModel { DisplayName = item.DisplayName, Name = item.Name });
                                }
                            }
                        }
                    }
                }
            }
        }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; set; }
        public async Task Save()
        {
            if (_flowMakerOption.Section is null)
            {
                return;
            }
            if (!Directory.Exists(_flowMakerOption.CustomPageRootDir))
            {
                Directory.CreateDirectory(_flowMakerOption.CustomPageRootDir);
            }
            var path = Path.Combine(_flowMakerOption.CustomPageRootDir, _flowMakerOption.Section + ".json");
            List<SpikeTab> spikeTabs = [];
            foreach (var item in Tabs)
            {
                spikeTabs.Add(_mapper.Map<SpikeTabViewModel, SpikeTab>(item));
            }
            var r = JsonSerializer.Serialize(spikeTabs);
            await File.WriteAllTextAsync(path, r);
            Edit = false;
            if (CurrentBox is not null)
            {
                CurrentBox.Editing = false;
            }
            CurrentBox = null;
            if (CurrentAction is not null)
            {
                CurrentAction.Editing = false;
            }
            CurrentAction = null;
            var list = InitMenu();
            ReloadMenuCommand?.Execute(list).Subscribe();
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
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入标签名称") { DefaultValue = "New Tab" + (Tabs.Count + 1), OwnerTitle = WindowTitle });
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
            var r = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入分组名称") { DefaultValue = "New Box" + (CurrentTab.Boxes.Count + 1), OwnerTitle = WindowTitle });
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
            if (!Edit)
            {
                return;
            }
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
                    Reload();

                    return;
                }
                else
                {
                    CurrentBox = spikeBoxViewModel;
                    CurrentBox.Editing = true;
                }
                SelectResizable(CurrentBox.Size);
            }
            Reload();

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
        public ObservableCollection<string> CustomGroups { get; set; } = [];
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
                CurrentBox.DisplayView(null);
            }
            else
            {
                var vm = _serviceProvider.GetKeyedService<ICustomPageInjectViewModel>(viewName);
                if (vm is ICustomPageViewModel sVm)
                {
                    CurrentBox.DisplayView(sVm);
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
                    var name = vm.Definition.Name;
                    string? configName = null;
                    if (vm.Definition.Name.Contains(':'))
                    {
                        var names = vm.Definition.Name.Split(':');
                        name = names[0];
                        configName = names[1];
                    }
                    var model = new SpikeActionViewModel()
                    {
                        DisplayName = vm.DisplayName ?? "BTN",
                        Name = name,
                        ConfigName = configName,
                        Category = vm.Definition.Category,
                        Type = vm.Definition.Type,
                    };
                    if (string.IsNullOrEmpty(configName))
                    {
                        var def = await _flowProvider.LoadFlowDefinitionAsync(vm.Definition.Category, name);
                        foreach (var item in def.Data)
                        {
                            if (item.IsInput)
                            {
                                var data = new SpikeInputViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
                                if (!string.IsNullOrWhiteSpace(item.OptionProviderName))
                                {
                                    var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(item.Type + ":" + item.OptionProviderName);
                                    if (pp is not null)
                                    {
                                        var options = await pp.GetOptions();
                                        foreach (var option in options)
                                        {
                                            data.Options.Add(new FlowStepOptionViewModel(option.Value, option.Name));
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var option in item.Options)
                                    {
                                        data.Options.Add(new FlowStepOptionViewModel(option.Name, option.DisplayName));
                                    }
                                }

                                if (data.Options.Count != 0)
                                {
                                    data.HasOption = true;
                                }
                                model.Inputs.Add(data);
                            }
                            if (item.IsOutput)
                            {
                                model.Outputs.Add(new SpikeOutputViewModel { DisplayName = item.DisplayName, Name = item.Name });
                            }
                        }
                    }
                    CurrentBox.Actions.Add(model);
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
                SelectResizable(CurrentAction.ButtonSize);

            }
            Reload();
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

        #region 执行
        public ReactiveCommand<SpikeActionViewModel, Unit> RunCommand { get; }
        public async Task Run(SpikeActionViewModel action)
        {
            ConfigDefinition? config = null;
            if (action.Type == DefinitionType.Flow)
            {
                config = new ConfigDefinition() { Category = action.Category, Name = action.Name, ConfigName = action.ConfigName ?? "", ErrorHandling = ErrorHandling.Terminate, Repeat = 1, Retry = 0, Timeout = 0 };
                foreach (var item in action.Inputs)
                {
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        throw new Exception($"{item.DisplayName}不能为空");
                    }
                    config.Data.Add(new NameValue(item.DisplayName, item.Value));
                }
            }
            else if (action.Type == DefinitionType.Config && !string.IsNullOrWhiteSpace(action.ConfigName))
            {
                config = await _flowProvider.LoadConfigDefinitionAsync(action.Category, action.Name, action.ConfigName);

                if (config is null)
                {
                    return;
                }
                foreach (var item in action.Inputs)
                {
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        throw new Exception($"{item.DisplayName}不能为空");
                    }
                    config.Data.Add(new NameValue(item.DisplayName, item.Value));
                }
            }
            else
            {
                return;
            }
            var result = await _flowManager.Run(config);
            foreach (var item in action.Outputs)
            {
                var data = result[0].Data.FirstOrDefault(c => c.Name == item.Name);
                item.Value = data?.Value;
            }
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

        public string? ViewGroup { get; set; }
        public string? ViewName { get; set; }
        public List<SpikeBoxCustomViewInput> Inputs { get; set; } = [];

        public SpikeMoveAndResizable Size { get; set; } = new SpikeMoveAndResizable();

        public List<SpikeAction> Actions { get; set; } = [];

    }
    public class SpikeBoxCustomViewInput
    {
        public required string DisplayName { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        public string? Value { get; set; }
        public bool HasOption { get; set; }
        public List<OptionDefinition> Options { get; set; } = [];
    }
    public class SpikeAction
    {
        public required string DisplayName { get; set; }
        public required string Category { get; set; }
        public required string Name { get; set; }
        public DefinitionType Type { get; set; }

        public SpikeResizable ActionSize { get; set; } = new SpikeResizable();


        public SpikeMoveAndResizable ButtonSize { get; set; } = new SpikeMoveAndResizable();


        public SpikeMoveAndResizable InputSize { get; set; } = new SpikeMoveAndResizable();


        public SpikeMoveAndResizable OutputSize { get; set; } = new SpikeMoveAndResizable();


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
        public string? ViewGroup { get; set; }

        [Reactive]
        public ICustomPageViewModel? SpikeViewModel { get; set; }
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
        public ObservableCollection<SpikeBoxCustomViewInputViewModel> Inputs { get; set; } = [];

        [Reactive]
        public ObservableCollection<SpikeActionViewModel> Actions { get; set; } = [];

        [Reactive]
        public RoutingState Router { get; set; } = new RoutingState();

        public void DisplayView(ICustomPageViewModel? spikeViewModel)
        {
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
    public class SpikeBoxCustomViewInputViewModel : ReactiveObject
    {
        [Reactive]
        public required string DisplayName { get; set; }
        [Reactive]
        public required string Name { get; set; }
        [Reactive]
        public required string Type { get; set; }
        [Reactive]
        public string? Value { get; set; }
        [Reactive]
        public bool HasOption { get; set; }
        public List<OptionDefinition> Options { get; set; } = [];
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
        public string? ConfigName { get; set; }
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
        public required string Name { get; set; }
        [Reactive]
        public string? Value { get; set; }
    }

    public class SpikeInputViewModel(string name, string displayName, string type, string? value = null) : ReactiveObject
    {
        [Reactive]
        public string Type { get; set; } = type;
        [Reactive]
        public string Name { get; set; } = name;
        /// <summary>
        /// 显示名称，描述
        /// </summary>
        [Reactive]
        public string DisplayName { get; set; } = displayName;

        [Reactive]
        public string? Value { get; set; } = value;
        [Reactive]
        public bool HasOption { get; set; }
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


    #endregion

}
