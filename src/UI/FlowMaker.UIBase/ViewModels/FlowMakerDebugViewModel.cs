using DynamicData;
using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Ty;
using Ty.Module.Configs;
using Ty.Services;
using Ty.ViewModels;
using Ty.ViewModels.CustomPages;

namespace FlowMaker.ViewModels;

public partial class FlowMakerDebugViewModel : ViewModelBase, ICustomPageViewModel, IAsyncDisposable
{
    private readonly FlowManager _flowManager;
    private readonly IFlowProvider _flowProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageBoxManager _messageBoxManager;
    private readonly FlowMakerOption _flowMakerOption;

    public static string Category => "默认";

    public static string Name => "Debug";
    [Input]
    [Description("流程类型")]
    [Reactive]
    public string? FlowCategory { get; set; }

    [Input]
    [Reactive]
    [Description("流程名称")]
    public string? FlowName { get; set; }

    [Input]
    [Reactive]
    [Description("配置名称")]
    public string? ConfigName { get; set; }


    public Guid? FlowInstanceId { get; set; }
    private readonly AsyncLock locker = new();

    [Reactive]
    public ObservableCollection<string> CustomLogs { get; set; } = [];
    [Reactive]
    public MonitorInfoViewModel? Model { get; set; }

    [Reactive]
    public bool CanDebug { get; set; }
    public FlowMakerDebugViewModel(FlowManager flowManager, IFlowProvider flowProvider, IServiceProvider serviceProvider, IOptions<FlowMakerOption> options, IMessageBoxManager messageBoxManager)
    {
        this._flowManager = flowManager;
        this._flowProvider = flowProvider;
        this._serviceProvider = serviceProvider;
        this._messageBoxManager = messageBoxManager;
        this._flowMakerOption = options.Value;
        CanDebug = _flowMakerOption.CanDebug;

        foreach (var item in _flowMakerOption.CustomLogViews)
        {
            CustomLogs.Add(item);
        }

        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
        AddDebugCommand = ReactiveCommand.Create<(MonitorInfoViewModel, MonitorStepInfoViewModel)>(c => AddDebug(c.Item1, c.Item2));
        RemoveDebugCommand = ReactiveCommand.Create<(MonitorInfoViewModel, MonitorStepInfoViewModel)>(c => RemoveDebug(c.Item1, c.Item2));

        RunCommand = ReactiveCommand.CreateFromTask(Run);
        StopCommand = ReactiveCommand.CreateFromTask(Stop);
        SendEventCommand = ReactiveCommand.CreateFromTask(SendEvent);
        SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfig);
        RemoveCommand = ReactiveCommand.CreateFromTask(Remove);
        EditFlowCommand = ReactiveCommand.CreateFromTask(EditFlow);
        ShowStepLogCommand = ReactiveCommand.Create<MonitorStepInfoViewModel>(ShowStepLog);
        ShowAllLogCommand = ReactiveCommand.Create(ShowAllLog);
        ChangePageTypeCommand = ReactiveCommand.Create(ChangePageType);

        this.WhenAnyValue(c => c.SelectedStepOnce).WhereNotNull().Subscribe(ShowStepOnceLog);
    }

    public CompositeDisposable? Disposables { get; set; }
    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];
    public override async Task Activate()
    {
        Disposables = [];
        MessageBus.Current.Listen<DebugInfo>().Subscribe(c =>
        {
            if (Model is null || Model?.Id != c.Id)
            {
                return;
            }
            //遍历所有的子步骤
            foreach (var item in Model.Steps)
            {
                void Continue(MonitorStepInfoViewModel step)
                {
                    if (step.Id == c.StepId)
                    {
                        if (c.Debugging)
                        {
                            step.Stop(null);
                        }
                        else
                        {
                            step.Start(DateTime.Now);
                        }
                        step.Debugging = c.Debugging;
                    }
                    foreach (var sub in step.Steps)
                    {
                        Continue(sub);
                    }
                }
                Continue(item);
            }
        })
        .DisposeWith(Disposables);

        MessageBus.Current.Listen<MonitorMessage>().ObserveOn(RxApp.TaskpoolScheduler).Subscribe(c =>
        {
            locker.Wait(() =>
            {
                if (c.Context.FlowIds.Length > 1)
                {
                    return;
                }
                var id = c.Context.FlowIds[0];
                if (Model?.Id != id)
                {
                    return;
                }
                var flow = Model;

                if (c.RunnerState == FlowState.Running)
                {
                    if (flow is null)
                    {
                        return;
                    }
                    flow.Running = true;
                    flow.CompleteCount = 0;
                    flow.Percent = 0;
                    flow.Timeout = c.Context.ConfigDefinition.Timeout;
                    flow.Retry = c.Context.ConfigDefinition.Retry;
                    flow.Repeat = c.Context.ConfigDefinition.Repeat;
                    flow.ErrorStop = c.Context.ConfigDefinition.ErrorStop;
                    if (DataDisplay is not null)
                    {
                        DataDisplay.Dispose();
                    }
                    DataDisplay = new DataDisplayViewModel(c.Context, StepId, Index);


                    Reset(flow.Steps);


                    var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                    if (mid is MonitorMiddleware monitor)
                    {
                        if (!monitor.PercentChange.IsDisposed)
                        {
                            monitor.PercentChange.Subscribe(c =>
                            {
                                flow.Percent = c;
                            }).DisposeWith(flow.StepChange);
                        }
                        if (!monitor.StepChange.IsDisposed)
                        {
                            monitor.StepChange.Subscribe(c =>
                        {

                            var steps = flow.Steps;

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
                            var step = steps.FirstOrDefault(v => v.Id == c.Step.Id);
                            if (step is not null)
                            {
                                if (c.StepOnce is not null)
                                {
                                    step.ErrorIndex = c.StepOnce.ErrorIndex;
                                    step.CurrentIndex = c.StepOnce.CurrentIndex;

                                    if (c.StepOnce.State == StepOnceState.Start && c.StepOnce.StartTime.HasValue)
                                    {
                                        step.Start(c.StepOnce.StartTime.Value);
                                    }
                                    if (c.StepOnce.EndTime.HasValue)
                                    {
                                        step.Stop(c.StepOnce.EndTime.Value);
                                    }
                                }
                                else
                                {
                                    step.Repeat = c.StepStatus.Repeat;
                                    step.Retry = c.StepStatus.Retry;
                                    step.State = c.StepStatus.State;
                                    step.Finally = c.StepStatus.Finally;
                                    step.ErrorHandling = c.StepStatus.ErrorHandling;
                                }
                            }

                            if (c.StepOnce is not null)
                            {
                                var stepLog = new StepLogViewModel
                                {
                                    Logs = [],
                                    StepId = c.Step.Id,
                                    Name = c.Step.DisplayName,
                                    State = c.StepOnce.State.ToString(),
                                    StartTime = c.StepOnce.StartTime,
                                    EndTime = c.StepOnce.EndTime,
                                    Inputs = c.StepOnce.Inputs,
                                    Outputs = c.StepOnce.Outputs,
                                    Index = c.StepOnce.Index,
                                };

                                DataDisplay.StepLogsCache.AddOrUpdate(stepLog);
                            }
                        }).DisposeWith(flow.StepChange);
                        }
                    }
                }
                if (c.RunnerState == FlowState.Complete || c.RunnerState == FlowState.Cancel || c.RunnerState == FlowState.Error)
                {
                    if (flow is not null)
                    {
                        flow.Running = false;
                        Stop(flow.Steps);

                        flow.StepChange?.Dispose();
                        void Stop(IList<MonitorStepInfoViewModel> steps)
                        {
                            foreach (var item in steps)
                            {
                                Stop(item.Steps);
                                item.Stop(null);
                            }
                        }

                        flow.Percent = 100;
                        flow.StepChange = [];
                    }
                }
            });
        })
        .DisposeWith(Disposables);

        await Task.CompletedTask;
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
    public async Task Load()
    {
        if (string.IsNullOrEmpty(FlowCategory) || string.IsNullOrEmpty(FlowName))
        {
            return;
        }
        var definition = await _flowProvider.LoadFlowDefinitionAsync(FlowCategory, FlowName);
        if (definition is null)
        {
            return;
        }

        Model = new(FlowCategory, FlowName)
        {
            Debug = true,
            Id = FlowInstanceId,
        };

        async Task SetFlowStepAsync(IList<MonitorStepInfoViewModel> models, IFlowDefinition flowDefinition)
        {
            foreach (var item in flowDefinition.Steps)
            {
                var step = new MonitorStepInfoViewModel
                {
                    Category = item.Category,
                    DisplayName = item.DisplayName,
                    Name = item.Name,
                    Id = item.Id,
                };
                if (item.Type == StepType.Normal)
                {
                    Model.TotalCount++;
                    models.Add(step);
                }
                else if (item.Type == StepType.Embedded && flowDefinition is FlowDefinition fde)
                {
                    var embedded = fde.EmbeddedFlows.First(c => c.StepId == item.Id);
                    models.Add(step);
                    await SetFlowStepAsync(step.Steps, embedded);
                }
                else
                {
                    var stepDefinition = await _flowProvider.GetStepDefinitionAsync(item.Category, item.Name);
                    if (stepDefinition is IFlowDefinition fd)
                    {
                        models.Add(step);
                        await SetFlowStepAsync(step.Steps, fd);
                    }
                }
            }
        }


        await SetFlowStepAsync(Model.Steps, definition);
        foreach (var item in definition.Data)
        {
            if (item.IsInput)
            {
                var data = new FlowConfigDataInputViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
                if (!string.IsNullOrWhiteSpace(item.OptionProviderName))
                {
                    var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(item.Type + ":" + item.OptionProviderName);
                    if (pp is not null)
                    {
                        await foreach (var option in pp.GetOptions())
                        {
                            data.Options.Add(new FlowStepOptionViewModel(option.Name, option.Value));
                        }
                    }
                }
                else
                {
                    foreach (var option in item.Options)
                    {
                        data.Options.Add(new FlowStepOptionViewModel(option.DisplayName, option.Name));
                    }
                }

                if (data.Options.Count != 0)
                {
                    data.HasOption = true;
                }
                Model.Data.Add(data);
            }
        }


        foreach (var item in _flowMakerOption.Middlewares)
        {
            Model.Middlewares.Add(new MiddlewareSelectViewModel(item.Name, item.Value));
        }
        if (!string.IsNullOrEmpty(ConfigName))
        {
            var config = await _flowProvider.LoadConfigDefinitionAsync(FlowCategory, FlowName, ConfigName);
            if (config is not null)
            {
                Model.DisplayName = $"{FlowCategory}:{FlowName}:{ConfigName}";
                Model.ConfigName = ConfigName;
                Model.Timeout = config.Timeout;
                Model.Retry = config.Retry;
                Model.Repeat = config.Repeat;
                Model.ErrorStop = config.ErrorStop;
                Model.LogView = config.LogView;
                if (Model.Data is not null)
                {
                    foreach (var item in Model.Data)
                    {
                        var data = config.Data.FirstOrDefault(c => c.Name == item.Name);
                        item.Value = data?.Value;
                    }
                }

                foreach (var item in Model.Middlewares)
                {
                    item.Selected = config.Middlewares.Contains(item.Value);
                }
            }
        }

        if (FlowInstanceId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(Model.LogView))
            {
                var vm = _serviceProvider.GetKeyedService<ILogInjectViewModel>(Model.LogView);
                if (vm is ILogViewModel viewModel)
                {
                    Model.DisplayView(viewModel);
                }
            }
            var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(FlowInstanceId.Value, "monitor");

            if (mid is MonitorMiddleware monitor)
            {
                monitor.StepChange.Subscribe(c =>
                 {
                     var steps = this.Model.Steps;

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
                     var step = steps.FirstOrDefault(v => v.Id == c.Step.Id);
                     if (step is not null && c.StepOnce is not null)
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
                 }).DisposeWith(Model.StepChange);
                monitor.PercentChange.Subscribe(c =>
                {
                    Model.Percent = c;
                }).DisposeWith(Model.StepChange);
            }
        }

        await Activate();
    }
    [Reactive]
    public DataDisplayViewModel? DataDisplay { get; set; }

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }
    public async Task Remove()
    {
        var r = await _messageBoxManager.Conform.Handle(new ConformInfo("确定删除吗？"));
        if (!r)
        {
            return;
        }
        if (Model is not null && Model.Id.HasValue)
        {
            await _flowManager.Stop(Model.Id.Value);
        }
        MessageBus.Current.SendMessage(this, "RemoveDebug");
    }

    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public async Task Run()
    {
        await Task.CompletedTask;
        RxApp.TaskpoolScheduler.Schedule(async () =>
        {
            var monitorInfoViewModel = Model;
            if (monitorInfoViewModel is null)
            {
                return;
            }
            var config = new ConfigDefinition
            {
                Category = monitorInfoViewModel.Category,
                ConfigName = monitorInfoViewModel.ConfigName,
                Name = monitorInfoViewModel.Name,
                LogView = monitorInfoViewModel.LogView,
                Timeout = monitorInfoViewModel.Timeout,
                ErrorStop = monitorInfoViewModel.ErrorStop,
                Repeat = monitorInfoViewModel.Repeat,
                Retry = monitorInfoViewModel.Retry,
            };
            if (monitorInfoViewModel.Data is not null)
            {
                foreach (var item in monitorInfoViewModel.Data)
                {
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        RxApp.MainThreadScheduler.Schedule(async () => await _messageBoxManager.Alert.Handle(new AlertInfo($"参数：{item.Name} 未填写")));
                        return;
                    }
                    config.Data.Add(new NameValue(item.Name, item.Value));
                }
            }

            foreach (var item in monitorInfoViewModel.Middlewares)
            {
                if (item.Selected)
                {
                    config.Middlewares.Add(item.Value);
                }
            }

            Reset(monitorInfoViewModel.Steps);
            monitorInfoViewModel.StepChange?.Dispose();
            monitorInfoViewModel.StepChange = [];
            try
            {
                var id = await _flowManager.Init(config);
                monitorInfoViewModel.Id = id;
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "debug");
                if (mid is DebugMiddleware debug)
                {
                    List<Guid> debugs = [];

                    //遍历所有的子步骤
                    foreach (var item in monitorInfoViewModel.Steps)
                    {
                        void AddDebugs(MonitorStepInfoViewModel step, List<Guid> debugs)
                        {
                            if (step.IsDebug && step.Id.HasValue)
                            {
                                debugs.Add(step.Id.Value);
                            }
                            foreach (var sub in step.Steps)
                            {
                                AddDebugs(sub, debugs);
                            }
                        }
                        AddDebugs(item, debugs);
                    }

                    debug.AddDebugs(id, debugs);
                }
                if (!string.IsNullOrWhiteSpace(monitorInfoViewModel.LogView))
                {
                    var vm = _serviceProvider.GetKeyedService<ILogInjectViewModel>(monitorInfoViewModel.LogView);
                    if (vm is ILogViewModel viewModel)
                    {
                        RxApp.MainThreadScheduler.Schedule(() =>
                        {
                            monitorInfoViewModel.DisplayView(viewModel);
                        });
                    }
                }
                await foreach (var item in _flowManager.Run(id)) { }

            }
            catch (Exception e)
            {
                RxApp.MainThreadScheduler.Schedule(async () =>
                {
                    await _messageBoxManager.Alert.Handle(new AlertInfo(e.Message));
                });
            }

        });

    }
    protected void Reset(IList<MonitorStepInfoViewModel> steps)
    {
        foreach (var item in steps)
        {
            item.StartTime = null;
            item.UsedTime = null;
            item.State = StepState.Wait;
            item.ErrorIndex = 0;
            item.CurrentIndex = 0;
            item.Repeat = 0;
            item.Retry = 0;
            item.Finally = false;
            item.ErrorHandling = ErrorHandling.Skip;

            Reset(item.Steps);
        }
    }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public async Task Stop()
    {
        var r = await _messageBoxManager.Conform.Handle(new ConformInfo("确定停止吗？"));
        if (!r)
        {
            return;
        }
        if (Model is not null && Model.Id.HasValue)
        {
            await _flowManager.Stop(Model.Id.Value);
        }
    }
    public ReactiveCommand<Unit, Unit> SendEventCommand { get; }
    public async Task SendEvent()
    {
        if (Model is not null && Model.Id.HasValue && !string.IsNullOrEmpty(Model.EventName))
        {
            await _flowManager.SendEvent(Model.Id.Value, Model.EventName, Model.EventData);
        }
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
    public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; set; }
    public async Task SaveConfig()
    {
        if (Model is null)
        {
            return;
        }
        var model = Model;
        if (string.IsNullOrEmpty(model.ConfigName))
        {
            var configName = await _messageBoxManager.Prompt.Handle(new PromptInfo("请输入名称"));
            if (configName.Ok)
            {
                ConfigName = configName.Value;
                model.ConfigName = configName.Value;
                model.DisplayName = $"{FlowCategory}:{FlowName}:{model.ConfigName}";
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
            LogView = model.LogView,
            ConfigName = model.ConfigName,
            ErrorStop = model.ErrorStop,
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
    }

    public ReactiveCommand<Unit, Unit> EditFlowCommand { get; }
    public async Task EditFlow()
    {
        var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
        await vm.Load(FlowCategory, FlowName);
        var title = "牛马编辑器" + " " + FlowCategory + " " + FlowName;
        _messageBoxManager.Window.Handle(new ModalInfo(title, vm) { OwnerTitle = null }).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
        {
            //LoadFlows();
        });
    }

    [Reactive]
    public Guid? StepId { get; set; }
    [Reactive]
    public string? Index { get; set; }
    [Reactive]
    public string LogName { get; set; } = "全部";
    [Reactive]
    public bool ShowLog { get; set; }
    public ReactiveCommand<MonitorStepInfoViewModel, Unit> ShowStepLogCommand { get; }
    public void ShowStepLog(MonitorStepInfoViewModel monitorStepInfoViewModel)
    {
        StepId = monitorStepInfoViewModel.Id;
        LogName = monitorStepInfoViewModel.DisplayName;
        Index = null;
        if (DataDisplay is not null)
        {
            DataDisplay.StepId = monitorStepInfoViewModel.Id;
            DataDisplay.Index = null;
        }
        ShowLog = true;
    }
    [Reactive]
    public StepLogViewModel? SelectedStepOnce { get; set; }
    public void ShowStepOnceLog(StepLogViewModel monitorStepInfoViewModel)
    {
        StepId = monitorStepInfoViewModel.StepId;
        Index = monitorStepInfoViewModel.Index;
        LogName = monitorStepInfoViewModel.Name + monitorStepInfoViewModel.Index;

        if (DataDisplay is not null)
        {
            DataDisplay.StepId = monitorStepInfoViewModel.StepId;
            DataDisplay.Index = monitorStepInfoViewModel.Index;
        }
        ShowLog = true;
    }

    public ReactiveCommand<Unit, Unit> ShowAllLogCommand { get; }
    public void ShowAllLog()
    {
        StepId = null;
        Index = null;
        LogName = "全部";
        if (DataDisplay is not null)
        {
            DataDisplay.StepId = null;
            DataDisplay.Index = null;
        }
        ShowLog = true;
    }

    [Reactive]
    public PageType PageType { get; set; }

    public ReactiveCommand<Unit, Unit> ChangePageTypeCommand { get; }
    public void ChangePageType()
    {
        //切换下一个PageType
        PageType = (PageType)(((int)PageType + 1) % Enum.GetValues<PageType>().Length);
    }

    public async ValueTask DisposeAsync()
    {
        if (Model is not null && Model.Id.HasValue)
        {
            await _flowManager.Stop(Model.Id.Value);
        }
    }
}

public class DataDisplayViewModel : ReactiveObject, IDisposable
{
    public DataDisplayViewModel(FlowContext flowContext, Guid? stepId, string? index)
    {
        StepId = stepId;
        Index = index;
        flowContext.Data.Connect()
                       .Transform(c => new FlowGlobeDataViewModel(c))
                     .SubscribeOn(RxApp.TaskpoolScheduler)
                       .ObserveOn(RxApp.MainThreadScheduler)
                     .Bind(out _data)
                     .DisposeMany()
                     .Subscribe()
                     .DisposeWith(Disposables);

        flowContext.WaitEvents.Connect()
                     .Transform(c => new WaitEventViewModel(c))
                     .SubscribeOn(RxApp.TaskpoolScheduler)
                     .ObserveOn(RxApp.MainThreadScheduler)
                   .Bind(out _waitEvents)
                   .DisposeMany()
                   .Subscribe()
                   .DisposeWith(Disposables);

        var filter = this.WhenAnyValue(c => c.StepId, c => c.Index)
               .Select(BuildFilter);

        flowContext.Logs.Connect()
                    .Filter(filter)
                     .Transform(c => new LogInfoViewModel(c))
                     .SubscribeOn(RxApp.TaskpoolScheduler)
                     .ObserveOn(RxApp.MainThreadScheduler)
                   .Bind(out _log)
                   .DisposeMany()
                   .Subscribe()
                   .DisposeWith(Disposables);

        StepLogsCache.Connect()
                     .SubscribeOn(RxApp.TaskpoolScheduler)
                     .ObserveOn(RxApp.MainThreadScheduler)
                   .Bind(out _stepLogs)
                   .DisposeMany()
                   .Subscribe()
                   .DisposeWith(Disposables);

    }

    private Func<LogInfo, bool> BuildFilter((Guid?, string?) input)
    {
        if (StepId.HasValue && !string.IsNullOrEmpty(Index))
        {
            return log => (input.Item1 == log.StepId && input.Item2 == log.Index);
        }
        else if (StepId.HasValue)
        {
            return log => input.Item1 == log.StepId;
        }
        else if (!string.IsNullOrEmpty(Index))
        {
            return log => input.Item2 == log.Index;
        }
        return log => true;
    }

    public void Dispose()
    {
        StepLogsCache.Clear();
        StepLogsCache.Dispose();
        Disposables.Dispose();
    }

    [Reactive]
    public Guid? StepId { get; set; }
    [Reactive]
    public string? Index { get; set; }
    public CompositeDisposable Disposables { get; set; } = [];
    public ReadOnlyObservableCollection<FlowGlobeDataViewModel> FlowGlobeData => _data;
    private readonly ReadOnlyObservableCollection<FlowGlobeDataViewModel> _data;
    public ReadOnlyObservableCollection<LogInfoViewModel> Log => _log;
    private readonly ReadOnlyObservableCollection<LogInfoViewModel> _log;
    public ReadOnlyObservableCollection<WaitEventViewModel> WaitEvents => _waitEvents;
    private readonly ReadOnlyObservableCollection<WaitEventViewModel> _waitEvents;
    public ReadOnlyObservableCollection<StepLogViewModel> StepLogs => _stepLogs;
    private readonly ReadOnlyObservableCollection<StepLogViewModel> _stepLogs;
    public SourceCache<StepLogViewModel, string> StepLogsCache = new SourceCache<StepLogViewModel, string>(c => c.StepId + "." + c.Index);

}

public class WaitEventViewModel(WaitEvent waitEvent) : ReactiveObject
{
    [Reactive]
    public string Name { get; set; } = waitEvent.Name;
    [Reactive]
    public bool NeedData { get; set; } = waitEvent.NeedData;
}

public class FlowGlobeDataViewModel(FlowGlobeData flowGlobeData) : ReactiveObject
{
    [Reactive]
    public bool IsInput { get; set; } = flowGlobeData.IsInput;
    [Reactive]
    public bool IsOutput { get; set; } = flowGlobeData.IsOutput;
    /// <summary>
    /// 名称
    /// </summary>
    [Reactive]
    public string Name { get; set; } = flowGlobeData.Name;
    /// <summary>
    /// 类型
    /// </summary>
    [Reactive]
    public string Type { get; set; } = flowGlobeData.Type;
    /// <summary>
    /// 值
    /// </summary>
    [Reactive]
    public string? Value { get; set; } = flowGlobeData.Value;
}

public class LogInfoViewModel(LogInfo logInfo) : ReactiveObject
{
    [Reactive]
    public string Log { get; set; } = logInfo.Log;
    [Reactive]
    public LogLevel Level { get; set; } = logInfo.LogLevel;
    [Reactive]
    public DateTime Time { get; set; } = logInfo.Time;
    [Reactive]
    public Guid StepId { get; set; } = logInfo.StepId;
    [Reactive]
    public string Index { get; set; } = logInfo.Index;
}

public enum PageType
{
    Tree,
    List,
}

public class FlowConfigDataInputViewModel(string name, string displayName, string type, string? value = null) : ReactiveObject
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
