using FlowMaker.Middlewares;
using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public class FlowMakerMonitorViewModel : ViewModelBase
{
    private readonly FlowManager _flowManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageBoxManager _messageBoxManager;
    private readonly IFlowProvider _flowProvider;

    [Reactive]
    public int ColCount { get; set; } = 3;
    [Reactive]
    public int RowCount { get; set; } = 1;
    public const int MaxColCount = 4;
    public ObservableCollection<FlowMakerDebugViewModel> Flows { get; set; } = [];
    public ObservableCollection<MonitorRunningViewModel> Runnings { get; set; } = [];
    private readonly FlowMakerOption _flowMakerOption;

    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];

    public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, IMessageBoxManager messageBoxManager, IFlowProvider flowProvider)
    {
        _flowManager = flowManager;
        _serviceProvider = serviceProvider;
        _messageBoxManager = messageBoxManager;
        _flowProvider = flowProvider;
        _flowMakerOption = options.Value;
        //DeleteCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Delete);


        CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
        RemoveCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Remove);
        ExecuteFlowCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(ExecuteFlow);
        RemoveConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RemoveConfig);
        RunConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RunConfig);
        LoadConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(LoadConfig);
        ShowLogCommand = ReactiveCommand.CreateFromTask<MonitorRunningViewModel>(ShowLog);


        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
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
    }

    public CompositeDisposable? Disposables { get; set; }
    private readonly AsyncLock locker = new();

    public override async Task Activate()
    {
        Disposables = [];
        await LoadFlows();

        MessageBus.Current.Listen<FlowMakerDebugViewModel>("RemoveDebug").Subscribe(c =>
        {
            Flows.Remove(c);
        }).DisposeWith(Disposables);

        MessageBus.Current.Listen<MonitorMessage>().Subscribe(c =>
        {
            var id = c.Context.FlowIds[0];
            var running = Runnings.FirstOrDefault(v => v.Id == c.Context.FlowIds[0]);
            if (running is null)
            {
                running = new MonitorRunningViewModel() { DisplayName = c.Context.FlowDefinition.Category + ":" + c.Context.FlowDefinition.Name, RunnerState = c.RunnerState, Id = c.Context.FlowIds[0], TotalCount = c.TotalCount };
                Runnings.Insert(0, running);
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                if (mid is MonitorStepOnceMiddleware monitor)
                {
                    running.StepChange = monitor.StepChange.Subscribe(c =>
                    {
                        if (c.StepOnce.State == StepOnceState.Start && c.StepOnce.StartTime.HasValue)
                        {
                            running.CompleteCount += 0.5;
                            running.Percent = running.CompleteCount / running.TotalCount * 100;
                        }
                        if (c.StepOnce.State == StepOnceState.Complete && c.StepOnce.EndTime.HasValue)
                        {
                            running.CompleteCount += 0.5;
                            running.Percent = running.CompleteCount / running.TotalCount * 100;
                        }
                        if (c.StepOnce.State == StepOnceState.Skip)
                        {
                            running.CompleteCount += 1;
                            running.Percent = running.CompleteCount / running.TotalCount * 100;
                        }
                    });
                }
            }
            else
            {
                running.RunnerState = c.RunnerState;
            }
        }).DisposeWith(Disposables);

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
    public IList<MenuItemViewModel> InitMenu()
    {
        List<MenuItemViewModel> menus = [];
        menus.Add(new MenuItemViewModel("创建流程") { Command = CreateCommand });

        return menus;
    }

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
        _messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
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
        Flows.Add(vm);
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

    public ReactiveCommand<MonitorRunningViewModel, Unit> ShowLogCommand { get; }
    public async Task ShowLog(MonitorRunningViewModel monitorRunningViewModel)
    {
        var vm = _serviceProvider.GetRequiredService<FlowMakerLogViewModel>();
        await vm.Load(monitorRunningViewModel.Id);
        _messageBoxManager.Window.Handle(new ModalInfo("牛马日志", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
        {
        });
    }
    #endregion



    //public ReactiveCommand<FlowMakerDebugViewModel, Unit> DeleteCommand { get; }
    //public void Delete(FlowMakerDebugViewModel monitorInfoViewModel)
    //{
    //    //monitorInfoViewModel.StepChange?.Dispose();
    //    Flows.Remove(monitorInfoViewModel);
    //}

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
    public RunnerState RunnerState { get; set; }

    public IDisposable? StepChange { get; set; }


}


public class MonitorInfoViewModel(string category, string name) : ReactiveObject, IScreen
{
    [Reactive]
    public bool ShowView { get; set; } = true;
    [Reactive]
    public RoutingState Router { get; set; } = new RoutingState();
    public IDisposable? StepChange { get; set; }
    [Reactive]
    public Guid? Id { get; set; }
    [Reactive]
    public bool Debug { get; set; }
    [Reactive]
    public bool Running { get; set; }

    [Reactive]
    public string? EventName { get; set; }
    [Reactive]
    public string? EventData { get; set; }

    [Reactive]
    public string DisplayName { get; set; } = $"{category}:{name}";
    [Reactive]
    public string Category { get; set; } = category;

    [Reactive]
    public string Name { get; set; } = name;

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
    public int Retry { get; set; }
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    [Reactive]
    public int Repeat { get; set; }
    /// <summary>
    /// 超时时间
    /// </summary>
    [Reactive]
    public int Timeout { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    [Reactive]
    public ErrorHandling ErrorHandling { get; set; }
    [Reactive]
    public ObservableCollection<InputDataViewModel> Data { get; set; } = [];
    [Reactive]
    public ObservableCollection<MonitorStepInfoViewModel> Steps { get; set; } = [];
    [Reactive]
    public ObservableCollection<MiddlewareSelectViewModel> Middlewares { get; set; } = [];

}
public class MonitorStepInfoViewModel : ReactiveObject
{
    [Reactive]
    public bool IsDebug { get; set; }

    [Reactive]
    public bool Debugging { get; set; }

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
    public Guid? Id { get; set; }
    [Reactive]
    public required string DisplayName { get; set; }
    [Reactive]
    public required string Category { get; set; }
    [Reactive]
    public required string Name { get; set; }
    [Reactive]
    public DefinitionType Type { get; set; }
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
    public bool Complete { get; set; }

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
    public ObservableCollection<MenuItemViewModel> Children { get; set; } = [];
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


public class ConfigDefinitionViewModel : ReactiveObject
{
    [Reactive]
    public string? Category { get; set; }
    [Reactive]
    public string? Name { get; set; }
    /// <summary>
    /// 流程的类别
    /// </summary>
    [Reactive]
    public string? FlowCategory { get; set; }
    /// <summary>
    /// 流程的名称
    /// </summary>
    [Reactive]
    public string? FlowName { get; set; }
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

    [Reactive]
    public int Timeout { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    [Reactive]
    public ErrorHandling ErrorHandling { get; set; }
    [Reactive]
    public ObservableCollection<InputDataViewModel> Data { get; set; } = [];
}

public class InputDataViewModel(string name, string displayName, string type, string? value = null) : ReactiveObject
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


public class FlowStepRuntimeViewModel(Guid id, string displayName, Guid? subFlowId = null)
{
    public Guid Id { get; set; } = id;
    public Guid? SubFlowId { get; set; } = subFlowId;
    public string DisplayName { get; set; } = displayName;
    public ObservableCollection<FlowStepRuntimeViewModel> Children { get; set; } = [];
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

