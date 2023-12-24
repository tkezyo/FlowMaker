﻿using FlowMaker;
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
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Test1.ViewModels;

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
    public ObservableCollection<MonitorInfoViewModel> Flows { get; set; } = [];
    private readonly FlowMakerOption _flowMakerOption;

    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];

    public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, IMessageBoxManager messageBoxManager, IFlowProvider flowProvider)
    {
        this._flowManager = flowManager;
        this._serviceProvider = serviceProvider;
        this._messageBoxManager = messageBoxManager;
        this._flowProvider = flowProvider;
        _flowMakerOption = options.Value;
        DeleteCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Delete);
        RunCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Run);
        StopCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Stop);
        LockCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Lock);
        AddDebugCommand = ReactiveCommand.Create<MonitorStepInfoViewModel>(AddDebug);
        RemoveDebugCommand = ReactiveCommand.Create<MonitorStepInfoViewModel>(RemoveDebug);


        CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);

        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
        this.WhenAnyValue(c => c.Flows.Count).Subscribe(c =>
        {
            if (c > MaxColCount)
            {
                ColCount = MaxColCount;
                RowCount = (c / ColCount) + (c % ColCount > 0 ? 1 : 0);
            }
            else
            {
                ColCount = c < 0 ? 1 : c;
                RowCount = 1;
            }
        });
    }

    public CompositeDisposable? Disposables { get; set; }

    public override async Task Activate()
    {
        Disposables = [];

        foreach (var item in _flowManager.RunningFlows)
        {
            var flow = Flows.FirstOrDefault(v => v.Id == item.Id);

            if (flow is null)
            {
                await Load(item.Context.FlowDefinition.Category, item.Context.FlowDefinition.Name, false, item.Context.FlowIds[0]);
            }
            flow = Flows.First(v => v.Id == item.Context.FlowIds[0]);
            flow.Running = true;

            if (flow is null || flow.StepChange is not null)
            {
                return;
            }
            var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(item.Id, "monitor");
            if (mid is MonitorStepOnceMiddleware monitor)
            {
                flow.StepChange = monitor.StepChange.Subscribe(c =>
                {
                    var steps = Flows.FirstOrDefault(v => v.Id == c.FlowIds[0])?.Steps;

                    foreach (var item in c.FlowIds.Skip(1))
                    {
                        if (steps is null)
                        {
                            return;
                        }
                        steps = steps.FirstOrDefault(v => v.Id == item)?.Steps;
                    }
                    if (steps is null)
                    {
                        return;
                    }
                    var step = steps.FirstOrDefault(v => v.Id == c.StepId);
                    if (step is not null)
                    {
                        if (c.StepOnce.State == StepOnceState.Start && c.StepOnce.StartTime.HasValue)
                        {
                            step.Start(c.StepOnce.StartTime.Value);
                        }
                        if (c.StepOnce.State == StepOnceState.Complete && c.StepOnce.EndTime.HasValue)
                        {
                            step.Stop(c.StepOnce.EndTime.Value);
                        }
                    }
                });
            }

        }

        var f = MessageBus.Current.Listen<MonitorMessage>().Subscribe(async c =>
        {
            if (c.Context.FlowIds.Length > 1)
            {
                return;
            }
            var id = c.Context.FlowIds[0];
            var flow = Flows.FirstOrDefault(v => v.Id == id);

            if (c.RunnerState == RunnerState.Running)
            {
                if (flow is null)
                {
                    await Load(c.Context.FlowDefinition.Category, c.Context.FlowDefinition.Name, false, id);
                }
                flow = Flows.First(v => v.Id == id);
                flow.Running = true;

                if (flow is null || flow.StepChange is not null)
                {
                    return;
                }
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                if (mid is MonitorStepOnceMiddleware monitor)
                {
                    flow.StepChange = monitor.StepChange.Subscribe(c =>
                    {
                        var steps = Flows.FirstOrDefault(v => v.Id == c.FlowIds[0])?.Steps;

                        foreach (var item in c.FlowIds.Skip(1))
                        {
                            if (steps is null)
                            {
                                return;
                            }
                            steps = steps.FirstOrDefault(v => v.Id == item)?.Steps;
                        }
                        if (steps is null)
                        {
                            return;
                        }
                        var step = steps.FirstOrDefault(v => v.Id == c.StepId);
                        if (step is not null)
                        {
                            if (c.StepOnce.State == StepOnceState.Start && c.StepOnce.StartTime.HasValue)
                            {
                                step.Start(c.StepOnce.StartTime.Value);
                            }
                            if (c.StepOnce.State == StepOnceState.Complete && c.StepOnce.EndTime.HasValue)
                            {
                                step.Stop(c.StepOnce.EndTime.Value);
                            }
                        }
                    });
                }
            }
            if (c.RunnerState == RunnerState.Complete)
            {
                if (flow is not null)
                {
                    flow.Running = false;
                    flow.StepChange?.Dispose();
                    if (!flow.Debug)
                    {
                        await Task.Delay(2000);
                        Flows.Remove(flow);
                    }
                }
            }

        });
        Disposables.Add(f);
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
                foreach (var item in flow.Configs)
                {
                    flow.Configs.Add(item);
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
        await Task.CompletedTask;
        _messageBoxManager.Window.Handle(new ModalInfo("牛马编辑器", vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
        {
            LoadFlows();
        });
    }
    #endregion

    /// <summary>
    /// 载入步骤或流程
    /// </summary>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="System.Exception"></exception>
    public async Task Load(string category, string name, bool debug = true, Guid? id = null)
    {
        var stepDefinition = await _flowProvider.GetStepDefinitionAsync(category, name);
        if (stepDefinition is not null)
        {
            MonitorInfoViewModel flow = new(category, name, stepDefinition is FlowDefinition ? DefinitionType.Flow : DefinitionType.Step)
            {
                Category = category,
                Debug = debug,
                Id = id
            };

            Flows.Add(flow);

            async Task SetFlowStepAsync(IList<MonitorStepInfoViewModel> models, FlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (!item.IsSubFlow)
                    {
                        models.Add(new MonitorStepInfoViewModel { Category = item.Category, DisplayName = item.DisplayName, Name = item.Name, Id = item.Id });
                    }
                    else
                    {
                        var stepDefinition = await _flowProvider.GetStepDefinitionAsync(item.Category, item.Name);
                        if (stepDefinition is FlowDefinition fd)
                        {
                            var sub = new MonitorStepInfoViewModel { Category = item.Category, DisplayName = item.DisplayName, Name = item.Name, Id = item.Id };
                            models.Add(sub);
                            await SetFlowStepAsync(sub.Steps, fd);
                        }
                    }
                }
            }

            if (flow.Type == DefinitionType.Flow)
            {
                if (stepDefinition is FlowDefinition flowDefinition)
                {
                    await SetFlowStepAsync(flow.Steps, flowDefinition);
                }
            }
            else
            {
                flow.Steps.Add(new MonitorStepInfoViewModel { Category = stepDefinition.Category, DisplayName = stepDefinition.Name, Name = stepDefinition.Name });
            }

            foreach (var item in stepDefinition.Data)
            {
                if (item.IsInput)
                {
                    var data = new InputDataViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
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
                    flow.Data.Add(data);
                }
            }
        }
        else
        {
            throw new Exception("未找到步骤");
        }
    }

    public ReactiveCommand<MonitorInfoViewModel, Unit> DeleteCommand { get; }
    public void Delete(MonitorInfoViewModel monitorInfoViewModel)
    {
        monitorInfoViewModel.StepChange?.Dispose();
        Flows.Remove(monitorInfoViewModel);
    }
    public ReactiveCommand<MonitorInfoViewModel, Unit> LockCommand { get; }
    public void Lock(MonitorInfoViewModel monitorInfoViewModel)
    {
        monitorInfoViewModel.Debug = true;
    }
    public ReactiveCommand<MonitorInfoViewModel, Unit> RunCommand { get; }
    public async Task Run(MonitorInfoViewModel monitorInfoViewModel)
    {
        var config = new ConfigDefinition
        {
            Category = monitorInfoViewModel.Category,
            ConfigName = monitorInfoViewModel.Name,
            Name = monitorInfoViewModel.Name,
            Timeout = monitorInfoViewModel.Timeout,
            ErrorHandling = monitorInfoViewModel.ErrorHandling,
            Repeat = monitorInfoViewModel.Repeat,
            Retry = monitorInfoViewModel.Retry,
        };
        foreach (var item in monitorInfoViewModel.Data)
        {
            if (string.IsNullOrEmpty(item.Value))
            {
                throw new Exception($"{item.Name}未填写");
            }
            config.Data.Add(new NameValue(item.Name, item.Value));
        }
        void Reset(IList<MonitorStepInfoViewModel> steps)
        {
            foreach (var item in steps)
            {
                item.UsedTime = default;
                Reset(item.Steps);
            }
        }
        Reset(monitorInfoViewModel.Steps);
        monitorInfoViewModel.StepChange?.Dispose();
        monitorInfoViewModel.StepChange = null;
        await _flowManager.Run(config, c =>
            {
                monitorInfoViewModel.Id = c;
            });
    }
    public ReactiveCommand<MonitorInfoViewModel, Unit> StopCommand { get; }
    public async Task Stop(MonitorInfoViewModel monitorInfoViewModel)
    {
        if (monitorInfoViewModel.Id.HasValue)
        {
            await _flowManager.Dispose(monitorInfoViewModel.Id.Value);
        }
    }
    public ReactiveCommand<MonitorStepInfoViewModel, Unit> AddDebugCommand { get; }
    public void AddDebug(MonitorStepInfoViewModel monitorInfoViewModel)
    {
        if (!monitorInfoViewModel.Id.HasValue)
        {
            return;
        }
        monitorInfoViewModel.IsDebug = true;
        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(monitorInfoViewModel.Id.Value, "debug");
        if (mid is DebugMiddleware debug)
        {
            debug.AddDebug(monitorInfoViewModel.Id.Value);
        }
    }
    public ReactiveCommand<MonitorStepInfoViewModel, Unit> RemoveDebugCommand { get; }
    public void RemoveDebug(MonitorStepInfoViewModel monitorInfoViewModel)
    {
        if (!monitorInfoViewModel.Id.HasValue)
        {
            return;
        }
        monitorInfoViewModel.IsDebug = false;
        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(monitorInfoViewModel.Id.Value, "debug");
        if (mid is DebugMiddleware debug)
        {
            debug.RemoveDebug(monitorInfoViewModel.Id.Value);
        }
    }
}


public class MonitorInfoViewModel(string category, string name, DefinitionType type) : ReactiveObject, IScreen
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
    public string DisplayName { get; set; } = $"{category}:{name} ({type})";
    [Reactive]
    public string Category { get; set; } = category;

    [Reactive]
    public string Name { get; set; } = name;
    [Reactive]
    public DefinitionType Type { get; set; } = type;

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
}
public class MonitorStepInfoViewModel : ReactiveObject
{
    [Reactive]
    public bool IsDebug { get; set; }

    [Reactive]
    public TimeSpan? UsedTime { get; set; }
    public DateTime StartTime { get; set; }
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
    public void Stop(DateTime endTime)
    {
        Timer?.Dispose();
        UsedTime = endTime - StartTime;
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
    public ObservableCollection<string> Configs { get; set; } = [];
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