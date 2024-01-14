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
using System.Text.Json;
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
    private readonly FlowMakerOption _flowMakerOption;

    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];
    public ObservableCollection<FlowMakerDebugViewModel> Flows { get; set; } = [];

    public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, IMessageBoxManager messageBoxManager, IFlowProvider flowProvider)
    {
        _flowManager = flowManager;
        _serviceProvider = serviceProvider;
        _messageBoxManager = messageBoxManager;
        _flowProvider = flowProvider;
        _flowMakerOption = options.Value;

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
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

    public bool Loaded { get; set; }
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

        MessageBus.Current.Listen<FlowMakerDebugViewModel>("RemoveDebug").Subscribe(c =>
        {
            Flows.Remove(c);
        }).DisposeWith(Disposables);
        MessageBus.Current.Listen<FlowMakerDebugViewModel>("AddDebug").Subscribe(Flows.Add).DisposeWith(Disposables);
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

    public IList<MenuItemViewModel> InitMenu()
    {
        List<MenuItemViewModel> menus = [];

        menus.Add(new MenuItemViewModel("保存") { Command = SaveCommand });

        return menus;
    }
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
        List<ConfigInfo> infos = [];
        foreach (var item in Flows)
        {
            if (string.IsNullOrEmpty(item.FlowCategory) || string.IsNullOrEmpty(item.FlowName) || string.IsNullOrEmpty(item.ConfigName))
            {
                await _messageBoxManager.Alert.Handle(new AlertInfo("必须先保存配置") { OwnerTitle = WindowTitle });
                return;
            }
            infos.Add(new ConfigInfo { Category = item.FlowCategory, ConfigName = item.ConfigName, Name = item.FlowName });
        }
        var r = JsonSerializer.Serialize(infos);
        await File.WriteAllTextAsync(path, r);
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
        var infos = new List<ConfigInfo>();
        try
        {
            var r = await File.ReadAllTextAsync(path);
            infos = JsonSerializer.Deserialize<List<ConfigInfo>>(r);
        }
        catch (Exception ex)
        {
            await _messageBoxManager.Alert.Handle(new AlertInfo(ex.Message) { OwnerTitle = WindowTitle });
            return;
        }
        if (infos is null)
        {
            return;
        }
        foreach (var item in infos)
        {
            var flow = _serviceProvider.GetRequiredService<FlowMakerDebugViewModel>();

            flow.FlowCategory = item.Category;
            flow.FlowName = item.Name;
            flow.ConfigName = item.ConfigName;
            await flow.Load();
            Flows.Add(flow);
        }
    }
}

public class ConfigInfo
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public required string ConfigName { get; set; }

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


}


public class MonitorInfoViewModel(string category, string name) : ReactiveObject, IScreen
{
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

    public CompositeDisposable StepChange { get; set; } = [];
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
    public ErrorHandling ErrorHandling { get; set; } = ErrorHandling.Skip;
    [Reactive]
    public ObservableCollection<SpikeInputViewModel> Data { get; set; } = [];
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
    public ObservableCollection<SpikeInputViewModel> Data { get; set; } = [];
}

//public class InputDataViewModel(string name, string displayName, string type, string? value = null) : ReactiveObject
//{
//    [Reactive]
//    public string Type { get; set; } = type;
//    [Reactive]
//    public string Name { get; set; } = name;
//    /// <summary>
//    /// 显示名称，描述
//    /// </summary>
//    [Reactive]
//    public string DisplayName { get; set; } = displayName;

//    [Reactive]
//    public string? Value { get; set; } = value;
//    [Reactive]
//    public bool HasOption { get; set; }
//    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];
//}


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

