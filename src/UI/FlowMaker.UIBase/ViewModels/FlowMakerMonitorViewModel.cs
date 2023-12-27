using FlowMaker;
using FlowMaker.Middlewares;
using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
    public ObservableCollection<MonitorInfoViewModel> Flows { get; set; } = [];
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
        DeleteCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Delete);
        RunCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Run);
        StopCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Stop);
        SendEventCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(SendEvent);
        LockCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Lock);
        AddDebugCommand = ReactiveCommand.Create<(MonitorInfoViewModel, MonitorStepInfoViewModel)>(c => AddDebug(c.Item1, c.Item2));
        RemoveDebugCommand = ReactiveCommand.Create<(MonitorInfoViewModel, MonitorStepInfoViewModel)>(c => RemoveDebug(c.Item1, c.Item2));


        CreateCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel?>(Create);
        RemoveCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(Remove);
        ExecuteFlowCommand = ReactiveCommand.CreateFromTask<FlowDefinitionInfoViewModel>(ExecuteFlow);
        SaveConfigCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(SaveConfig);
        RemoveConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RemoveConfig);
        RunConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(RunConfig);
        LoadConfigCommand = ReactiveCommand.CreateFromTask<ConfigDefinitionInfoViewModel>(LoadConfig);


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
        foreach (var item in _flowManager.RunningFlows)
        {
            var flow = Flows.FirstOrDefault(v => v.Id == item.Id);

            if (flow is null)
            {
                await Load(item.Context.FlowDefinition.Category, item.Context.FlowDefinition.Name, item.Context.ConfigDefinition.ConfigName, false, item.Context.FlowIds[0]);
            }
            flow = Flows.First(v => v.Id == item.Context.FlowIds[0]);
            flow.Running = true;
            flow.CompleteCount = 0;
            flow.Timeout = item.Context.ConfigDefinition.Timeout;
            flow.Retry = item.Context.ConfigDefinition.Retry;
            flow.Repeat = item.Context.ConfigDefinition.Repeat;
            flow.ErrorHandling = item.Context.ConfigDefinition.ErrorHandling;

            if (flow is null || flow.StepChange is not null)
            {
                return;
            }
            var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(item.Id, "monitor");
            if (mid is MonitorMiddleware monitor)
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
                            flow.CompleteCount += 0.5;

                            flow.Percent = (double)flow.CompleteCount / flow.TotalCount * 100;
                            step.Start(c.StepOnce.StartTime.Value);
                        }
                        if (c.StepOnce.State == StepOnceState.Complete && c.StepOnce.EndTime.HasValue)
                        {
                            flow.CompleteCount += 0.5;

                            flow.Percent = (double)flow.CompleteCount / flow.TotalCount * 100;
                            step.Stop(c.StepOnce.EndTime.Value);
                        }
                    }
                });
            }

        }
        MessageBus.Current.Listen<DebugInfo>().Subscribe(c =>
          {
              var flow = Flows.FirstOrDefault(v => v.Id == c.Id);
              if (flow is null)
              {
                  return;
              }
              var step = flow.Steps.FirstOrDefault(v => v.Id == c.StepId);
              if (step is null)
              {
                  return;
              }
              if (c.Debugging)
              {
                  step.Stop(null);
              }
              else
              {
                  step.Start(DateTime.Now);
              }
              step.Debugging = c.Debugging;

          })
            .DisposeWith(Disposables);

        MessageBus.Current.Listen<MonitorMessage>().Subscribe(c =>
          {
              locker.Wait(async () =>
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
                          await Load(c.Context.FlowDefinition.Category, c.Context.FlowDefinition.Name, c.Context.ConfigDefinition.ConfigName, false, id);
                      }
                      flow = Flows.First(v => v.Id == id);
                      flow.Running = true;
                      flow.CompleteCount = 0;
                      flow.Timeout = c.Context.ConfigDefinition.Timeout;
                      flow.Retry = c.Context.ConfigDefinition.Retry;
                      flow.Repeat = c.Context.ConfigDefinition.Repeat;
                      flow.ErrorHandling = c.Context.ConfigDefinition.ErrorHandling;
                      if (flow is null || flow.StepChange is not null)
                      {
                          return;
                      }
                      var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                      if (mid is MonitorMiddleware monitor)
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
                                      flow.CompleteCount += 0.5;
                                      flow.Percent = (double)flow.CompleteCount / flow.TotalCount * 100;
                                      step.Start(c.StepOnce.StartTime.Value);
                                  }
                                  if (c.StepOnce.State == StepOnceState.Complete && c.StepOnce.EndTime.HasValue)
                                  {
                                      flow.CompleteCount += 0.5;
                                      flow.Percent = (double)flow.CompleteCount / flow.TotalCount * 100;
                                      step.Stop(c.StepOnce.EndTime.Value);
                                  }
                              }
                          });
                      }
                  }
                  if (c.RunnerState == RunnerState.Complete || c.RunnerState == RunnerState.Cancel)
                  {
                      if (flow is not null)
                      {
                          flow.Running = false;

                          flow.StepChange?.Dispose();
                          foreach (var item in flow.Steps)
                          {
                              item.Stop(null);
                          }
                          flow.Percent = 100;
                          flow.StepChange = null;
                          if (!flow.Debug)
                          {
                              await Task.Delay(2000);
                              Flows.Remove(flow);
                          }
                      }
                  }
              });


          })
            .DisposeWith(Disposables);
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
        await Task.CompletedTask;
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
        await Load(flowDefinitionInfoViewModel.Category, flowDefinitionInfoViewModel.Name, null, true);
    }


    public ReactiveCommand<MonitorInfoViewModel, Unit> SaveConfigCommand { get; set; }
    public async Task SaveConfig(MonitorInfoViewModel model)
    {
        if (string.IsNullOrEmpty(model.ConfigName))
        {
            var configName = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入名称"));
            if (configName.Ok)
            {
                model.ConfigName = configName.Value;
            }
        }
        if (string.IsNullOrEmpty(model.ConfigName))
        {
            return;
        }
        ConfigDefinition configDefinition = new()
        {
            Category = model.Category,
            Name = model.Name,
            ConfigName = model.ConfigName,
            ErrorHandling = model.ErrorHandling,
            Repeat = model.Repeat,
            Retry = model.Retry,
        };
        foreach (var item in model.Data)
        {
            if (string.IsNullOrEmpty(item.Value))
            {
                return;
            }
            configDefinition.Data.Add(new NameValue(item.Name, item.Value));
        }
        foreach (var item in model.Middlewares)
        {
            if (item.Selected)
            {
                configDefinition.Middlewares.Add(item.Value);
            }
        }
        await _flowProvider.SaveConfig(configDefinition);
        await LoadFlows();
    }

    public ReactiveCommand<ConfigDefinitionInfoViewModel, Unit> LoadConfigCommand { get; set; }
    public async Task LoadConfig(ConfigDefinitionInfoViewModel model)
    {
        await Load(model.Category, model.Name, model.ConfigName, true);
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


    /// <summary>
    /// 载入步骤或流程
    /// </summary>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task Load(string category, string name, string? configName, bool debug = true, Guid? id = null)
    {
        var stepDefinition = await _flowProvider.GetStepDefinitionAsync(category, name);
        if (stepDefinition is null)
        {
            throw new Exception("未找到步骤");
        }
        if (stepDefinition is not FlowDefinition flowDefinition)
        {
            throw new Exception("非流程");
        }
        MonitorInfoViewModel flow = new(category, name)
        {
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
                    flow.TotalCount++;
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


        await SetFlowStepAsync(flow.Steps, flowDefinition);

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

        foreach (var item in _flowMakerOption.Middlewares)
        {
            flow.Middlewares.Add(new MiddlewareSelectViewModel(item.Name, item.Value));
        }
        if (!string.IsNullOrEmpty(configName))
        {
            var config = await _flowProvider.LoadConfigDefinitionAsync(category, name, configName);
            if (config is not null)
            {
                flow.DisplayName = $"{category}:{name}";
                flow.ConfigName = configName;
                flow.Timeout = config.Timeout;
                flow.Retry = config.Retry;
                flow.Repeat = config.Repeat;
                flow.ErrorHandling = config.ErrorHandling;
                foreach (var item in flow.Data)
                {
                    var data = config.Data.FirstOrDefault(c => c.Name == item.Name);
                    item.Value = data?.Value;
                }
                foreach (var item in flow.Middlewares)
                {
                    item.Selected = config.Middlewares.Contains(item.Value);
                }
            }
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
            ConfigName = monitorInfoViewModel.ConfigName,
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
        foreach (var item in monitorInfoViewModel.Middlewares)
        {
            if (item.Selected)
            {
                config.Middlewares.Add(item.Value);
            }
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
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(c, "debug");
                if (mid is DebugMiddleware debug)
                {
                    debug.AddDebugs(c, monitorInfoViewModel.Steps.Where(c => c.IsDebug && c.Id.HasValue).Select(c => c.Id!.Value).ToList());
                }
                monitorInfoViewModel.Id = c;
            });
    }
    public ReactiveCommand<MonitorInfoViewModel, Unit> StopCommand { get; }
    public async Task Stop(MonitorInfoViewModel monitorInfoViewModel)
    {
        if (monitorInfoViewModel.Id.HasValue)
        {
            await _flowManager.Stop(monitorInfoViewModel.Id.Value);
        }
    }
    public ReactiveCommand<MonitorInfoViewModel, Unit> SendEventCommand { get; }
    public async Task SendEvent(MonitorInfoViewModel monitorInfoViewModel)
    {
        if (monitorInfoViewModel.Id.HasValue && !string.IsNullOrEmpty(monitorInfoViewModel.EventName))
        {
            _flowManager.SendEvent(monitorInfoViewModel.Id.Value, monitorInfoViewModel.EventName, monitorInfoViewModel.EventData);
        }
        await Task.CompletedTask;
    }
    public ReactiveCommand<(MonitorInfoViewModel, MonitorStepInfoViewModel), Unit> AddDebugCommand { get; }
    public void AddDebug(MonitorInfoViewModel monitorInfoViewModel, MonitorStepInfoViewModel monitorStepInfoViewModel)
    {
        monitorStepInfoViewModel.IsDebug = true;
        if (!monitorInfoViewModel.Id.HasValue || !monitorStepInfoViewModel.Id.HasValue)
        {
            return;
        }
        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(monitorInfoViewModel.Id.Value, "debug");
        if (mid is DebugMiddleware debug)
        {
            debug.AddDebug(monitorInfoViewModel.Id.Value, monitorStepInfoViewModel.Id.Value);
        }
    }
    public ReactiveCommand<(MonitorInfoViewModel, MonitorStepInfoViewModel), Unit> RemoveDebugCommand { get; }
    public void RemoveDebug(MonitorInfoViewModel monitorInfoViewModel, MonitorStepInfoViewModel monitorStepInfoViewModel)
    {
        monitorStepInfoViewModel.IsDebug = false;
        if (!monitorInfoViewModel.Id.HasValue || !monitorStepInfoViewModel.Id.HasValue)
        {
            return;
        }
        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(monitorInfoViewModel.Id.Value, "debug");
        if (mid is DebugMiddleware debug)
        {
            debug.RemoveDebug(monitorInfoViewModel.Id.Value, monitorStepInfoViewModel.Id.Value);
        }
    }
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

