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
using System.Text.Json;
using System.Windows.Input;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public class FlowMakerMainViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new RoutingState();
    private readonly FlowMakerOption _flowMakerOption;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowManager _flowManager;
    private readonly IMessageBoxManager _messageBoxManager;
    private readonly IFlowProvider _flowProvider;
    [Reactive]
    public int ColCount { get; set; } = 3;
    [Reactive]
    public int RowCount { get; set; } = 1;
    [Reactive]
    public int MaxColCount { get; set; }


    public ObservableCollection<FlowMakerDebugViewModel> Flows { get; set; } = [];
    public FlowMakerMainViewModel(IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, FlowManager flowManager, IMessageBoxManager messageBoxManager, IFlowProvider flowProvider)
    {
        _flowMakerOption = options.Value;
        _serviceProvider = serviceProvider;
        _flowManager = flowManager;
        this._messageBoxManager = messageBoxManager;
        this._flowProvider = flowProvider;
        MaxColCount = _flowMakerOption.MaxColCount;

        ShowLogCommand = ReactiveCommand.CreateFromTask<MonitorRunningViewModel>(ShowLog);


        CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
        RemoveCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Remove);
        ExecuteFlowCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(ExecuteFlow);
        RemoveConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RemoveConfig);
        RunConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RunConfig);
        LoadConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(LoadConfig);

        RemoveDebugCommand = ReactiveCommand.CreateFromTask<FlowMakerDebugViewModel>(RemoveDebugAsync);

        LoadFlowsCommand = ReactiveCommand.CreateFromTask(LoadFlows);

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        this.WhenAnyValue(c => c.Flows.Count).Subscribe(c =>
        {
            if (c > MaxColCount)
            {
                ColCount = MaxColCount;
                RowCount = c / ColCount + (c % ColCount > 0 ? 1 : 0);
            }
            else
            {
                ColCount = c < 0 ? 1 : c;
                RowCount = 1;
            }
        });

        this.WhenAnyValue(c => c.ShowLogList).Where(c => c).Subscribe(c => Edit = false);
        this.WhenAnyValue(c => c.Edit).Where(c => c).Subscribe(async c =>
        {
            ShowLogList = false;

            await LoadFlows();
        });

    }

    public CompositeDisposable? Disposables { get; set; }

    public bool Loaded { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public async Task SaveAsync()
    {
        if (_flowMakerOption.Section is null)
        {
            return;
        }
        if (!Directory.Exists(_flowMakerOption.DebugPageRootDir))
        {
            Directory.CreateDirectory(_flowMakerOption.DebugPageRootDir);
        }
        var path = Path.Combine(_flowMakerOption.DebugPageRootDir, _flowMakerOption.Section + ".json");
        List<ConfigInfo> configInfo = [];


        foreach (var item in Flows)
        {
            if (string.IsNullOrEmpty(item.FlowCategory) || string.IsNullOrEmpty(item.FlowName))
            {
                return;
            }
            configInfo.Add(new ConfigInfo { Category = item.FlowCategory, ConfigName = item.ConfigName, Name = item.FlowName });
        }
        var r = JsonSerializer.Serialize(configInfo);
        await File.WriteAllTextAsync(path, r);
        await _messageBoxManager.Notify.Handle(new NotifyInfo() { Message = "保存成功", Level = NotifyLevel.Success, Expiration = TimeSpan.FromSeconds(2) });
    }

    public async Task Load()
    {
        if (!Directory.Exists(_flowMakerOption.DebugPageRootDir))
        {
            Directory.CreateDirectory(_flowMakerOption.DebugPageRootDir);
        }
        var path = Path.Combine(_flowMakerOption.DebugPageRootDir, _flowMakerOption.Section + ".json");
        if (!File.Exists(path))
        {
            return;
        }
        List<ConfigInfo>? configInfo;
        try
        {
            var r = await File.ReadAllTextAsync(path);
            configInfo = JsonSerializer.Deserialize<List<ConfigInfo>>(r);
        }
        catch (Exception ex)
        {
            await _messageBoxManager.Alert.Handle(new AlertInfo(ex.Message) { OwnerTitle = WindowTitle });
            return;
        }
        if (configInfo is null)
        {
            return;
        }
        foreach (var item in configInfo)
        {
            var flow = _serviceProvider.GetRequiredService<FlowMakerDebugViewModel>();

            flow.FlowCategory = item.Category;
            flow.FlowName = item.Name;
            flow.ConfigName = item.ConfigName;
            try
            {
                await flow.Load();
                Flows.Add(flow);
            }
            catch (Exception e)
            {
                await _messageBoxManager.Alert.Handle(new AlertInfo(e.Message));
            }

        }
    }


    public ObservableCollection<MonitorRunningViewModel> Runnings { get; set; } = [];

    public override async Task Activate()
    {
        Disposables = [];
        if (!Loaded)
        {
            await Load();
            Loaded = true;
        }
        if (_flowMakerOption.AutoRun)
        {
            foreach (var item in Flows)
            {
                _ = item.Run();
            }
        }

        MessageBus.Current.Listen<MenuViewModel>("Menu").Subscribe(c =>
        {
            if (c.Name == "FlowMakerEdit")
            {
                Edit = !Edit;
            }
        }).DisposeWith(Disposables);


        MessageBus.Current.Listen<FlowMakerDebugViewModel>("RemoveDebug").Subscribe(c =>
        {
            Flows.Remove(c);
        }).DisposeWith(Disposables);


        await LoadFlows();

        MessageBus.Current.Listen<MonitorMessage>().ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
        {
            var id = c.Context.FlowIds[0];
            var running = Runnings.FirstOrDefault(v => v.Id == c.Context.FlowIds[0]);
            if (running is null)
            {
                running = new()
                {
                    DisplayName = DateTime.Now.ToString("HH:mm:ss") + "|" + c.Context.ConfigDefinition.Category + "." + c.Context.ConfigDefinition.Name,
                    RunnerState = c.Context.State,
                    Id = c.Context.FlowIds[0],
                    TotalCount = c.TotalCount,
                    StartTime = DateTime.Now
                };
                Runnings.Insert(0, running);
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, MonitorMiddleware.Name);
                if (mid is MonitorMiddleware monitor)
                {
                    if (!monitor.PercentChange.IsDisposed)
                    {
                        running.StepChange = monitor.PercentChange.Subscribe(c =>
                        {
                            running.Percent = c;
                        });
                    }
                }
            }
            else
            {
                running.RunnerState = c.Context.State;
            }
        }).DisposeWith(Disposables);

    }


    public ReactiveCommand<MonitorRunningViewModel, Unit> ShowLogCommand { get; }
    public async Task ShowLog(MonitorRunningViewModel monitorRunningViewModel)
    {
        var vm = _serviceProvider.GetRequiredService<FlowMakerLogViewModel>();
        await vm.Load(monitorRunningViewModel.Id);
        _messageBoxManager.Window.Handle(new ModalInfo("牛马日志", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe();
    }
    public override Task Deactivate()
    {
        if (Disposables is not null)
        {
            Disposables.Dispose();
            Disposables = null;
        }
        return base.Deactivate();
    }

    #region FlowTree

    public ObservableCollection<FlowCategoryViewModel> Categories { get; set; } = [];
    public ReactiveCommand<Unit, Unit> LoadFlowsCommand { get; }
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
        var r = await _messageBoxManager.Conform.Handle(new ConformInfo($"是否删除{flowDefinitionInfoViewModel.Name}"));
        if (!r)
        {
            return;
        }
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

        Flows.Add(vm);
    }

    [Reactive]
    public bool Edit { get; set; }
    [Reactive]
    public bool ShowLogList { get; set; }

    public ReactiveCommand<FlowMakerDebugViewModel, Unit> RemoveDebugCommand { get; }
    public async Task RemoveDebugAsync(FlowMakerDebugViewModel flowMakerDebugViewModel)
    {
        var r = await _messageBoxManager.Conform.Handle(new ConformInfo("确定删除吗？"));
        if (!r)
        {
            return;
        }
        if (flowMakerDebugViewModel.Model is not null && flowMakerDebugViewModel.Model.Id.HasValue)
        {
            await _flowManager.Dispose(flowMakerDebugViewModel.Model.Id.Value);
        }
        Flows.Remove(flowMakerDebugViewModel);
    }



    public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> LoadConfigCommand { get; set; }
    public async Task LoadConfig(ConfigDefinitionInfoViewModel model)
    {
        var vm = _serviceProvider.GetRequiredService<FlowMakerDebugViewModel>();
        vm.FlowCategory = model.Category;
        vm.FlowName = model.Name;
        vm.ConfigName = model.ConfigName;
        await vm.Load();
        Flows.Add(vm);
    }

    public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RemoveConfigCommand { get; }
    public async Task RemoveConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
    {
        var r = await _messageBoxManager.Conform.Handle(new ConformInfo($"是否删除{flowDefinitionInfoViewModel.ConfigName}"));
        if (!r)
        {
            return;
        }
        await _flowProvider.RemoveConfig(
               flowDefinitionInfoViewModel.ConfigName,
               flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name);
        await LoadFlows();
    }
    public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> RunConfigCommand { get; }
    public async Task RunConfig(ConfigDefinitionInfoViewModel flowDefinitionInfoViewModel)
    {
        await foreach (var item in _flowManager.Run(
            flowDefinitionInfoViewModel.ConfigName,
            flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name))
        {

        }
    }


    #endregion
}


public class ConfigInfo
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public string? ConfigName { get; set; }

}

public class MonitorRunningViewModel : ReactiveObject
{
    [Reactive]
    public Guid Id { get; set; }
    [Reactive]
    public required string DisplayName { get; set; }
    /// <summary>
    /// 完成个数
    /// </summary>
    [Reactive]
    public double CompleteCount { get; set; }
    /// <summary>
    /// 总个数
    /// </summary>
    [Reactive]
    public int TotalCount { get; set; }
    /// <summary>
    /// 完成比例
    /// </summary>
    [Reactive]
    public double Percent { get; set; }
    [Reactive]
    public FlowState RunnerState { get; set; }

    public IDisposable? StepChange { get; set; }
    [Reactive]
    public DateTime StartTime { get; set; }

}


public class MonitorInfoViewModel : ReactiveObject, IScreen
{
    public MonitorInfoViewModel(string category, string name)
    {
        DisplayName = $"{category}:{name}";
        Category = category;
        Name = name;

    }

    [Reactive]
    public bool ShowView { get; set; } = true;
    [Reactive]
    public string? LogView { get; set; }
    [Reactive]
    public RoutingState Router { get; set; } = new RoutingState();
    public void DisplayView(ILogViewModel logViewModel)
    {
        if (logViewModel is ViewModelBase modelBase)
        {
            ShowView = true;
            modelBase.SetScreen(this);
            logViewModel.Load(Id ?? Guid.Empty);
            Router.Navigate.Execute(modelBase);
        }
        else
        {
            ShowView = false;
        }
    }

    #region 单步调试
    [Reactive]
    public bool SingleRun { get; set; }

    #endregion


    public CompositeDisposable StepChange { get; set; } = [];
    [Reactive]
    public Guid? Id { get; set; }
    //[Reactive]
    //public bool Debug { get; set; }
    [Reactive]
    public bool Running { get; set; }

    [Reactive]
    public string? EventName { get; set; }
    [Reactive]
    public string? EventData { get; set; }

    [Reactive]
    public string DisplayName { get; set; }
    [Reactive]
    public string Category { get; set; }

    [Reactive]
    public string Name { get; set; }

    [Reactive]
    public string? ConfigName { get; set; }

    /// <summary>
    /// 完成个数
    /// </summary>
    [Reactive]
    public double CompleteCount { get; set; }
    /// <summary>
    /// 总个数
    /// </summary>
    [Reactive]
    public int TotalCount { get; set; }
    /// <summary>
    /// 完成比例
    /// </summary>
    [Reactive]
    public double Percent { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    [Reactive]
    public int Retry { get; set; } = 0;
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    [Reactive]
    public int Repeat { get; set; } = 1;
    /// <summary>
    /// 超时时间
    /// </summary>
    [Reactive]
    public int Timeout { get; set; } = 0;
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    [Reactive]
    public bool ErrorStop { get; set; }
    [Reactive]
    public ObservableCollection<FlowConfigDataInputViewModel> Data { get; set; } = [];
    [Reactive]
    public ObservableCollection<MonitorStepInfoViewModel> Steps { get; set; } = [];
    [Reactive]
    public ObservableCollection<MiddlewareSelectViewModel> Middlewares { get; set; } = [];


}
public class MonitorStepInfoViewModel : ReactiveObject
{
    [Reactive]
    public MonitorStepInfoViewModel? Parent { get; set; }
    [Reactive]
    public bool IsDebug { get; set; }

    [Reactive]
    public bool Debugging { get; set; }

    public required Guid[] ParentIds { get; set; }

    public CancellationTokenSource? SingleRunCancellationToken { get; set; }
    [Reactive]
    public bool SingleRunning { get; set; }

    [Reactive]
    public TimeSpan? UsedTime { get; set; }
    public DateTime? StartTime { get; set; }
    public IDisposable? Timer { get; set; }
    public void Start(DateTime startTime)
    {
        StartTime = startTime;
        Timer?.Dispose();
        Timer = Observable.Interval(TimeSpan.FromMilliseconds(20)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
        {
            UsedTime = DateTime.Now - StartTime;
        });
    }
    public void Stop(DateTime? endTime)
    {
        Timer?.Dispose();
        if (endTime.HasValue)
        {
            UsedTime = endTime - StartTime;
        }
        else
        {
            if (!StartTime.HasValue)
            {
                UsedTime = TimeSpan.Zero;
            }
        }
    }
    [Reactive]
    public required Guid Id { get; set; }
    [Reactive]
    public required string DisplayName { get; set; }
    [Reactive]
    public required string Category { get; set; }
    [Reactive]
    public required string Name { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    [Reactive]
    public int Retry { get; set; }
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    [Reactive]
    public int Repeat { get; set; }
    /// <summary>
    /// 当前下标
    /// </summary>
    [Reactive]
    public int CurrentIndex { get; set; }
    /// <summary>
    /// 执行错误下标
    /// </summary>
    [Reactive]
    public int ErrorIndex { get; set; }
    /// <summary>
    /// 是否完成
    /// </summary>
    [Reactive]
    public StepState State { get; set; }
    [Reactive]
    public ErrorHandling ErrorHandling { get; set; }
    [Reactive]
    public bool Finally { get; set; }

    public required FlowStep Step { get; set; }

    [Reactive]
    public ObservableCollection<MonitorStepInfoViewModel> Steps { get; set; } = [];
}


public class MenuItemViewModel(string name) : ReactiveObject
{
    [Reactive]
    public string Name { get; set; } = name;

    [Reactive]
    public ICommand? Command { get; set; }
    [Reactive]
    public object? CommandParameter { get; set; }
}
public class FlowCategoryViewModel(string category) : ReactiveObject
{
    [Reactive]
    public string Category { get; set; } = category;
    [Reactive]
    public ObservableCollection<FlowDefinitionInfoViewModel> Flows { get; set; } = [];

}
public class FlowDefinitionInfoViewModel(string category, string name) : ReactiveObject
{
    [Reactive]
    public string Category { get; set; } = category;
    [Reactive]
    public string Name { get; set; } = name;
    [Reactive]
    public ObservableCollection<ConfigDefinitionInfoViewModel> Configs { get; set; } = [];
}
public class ConfigDefinitionInfoViewModel(string category, string name, string configName) : FlowDefinitionInfoViewModel(category, name)
{

    [Reactive]
    public string ConfigName { get; set; } = configName;
}


public class MiddlewareSelectViewModel(string name, string value) : ReactiveObject
{
    [Reactive]
    public string Name { get; set; } = name;

    [Reactive]
    public string Value { get; set; } = value;
    [Reactive]
    public bool Selected { get; set; }
}
