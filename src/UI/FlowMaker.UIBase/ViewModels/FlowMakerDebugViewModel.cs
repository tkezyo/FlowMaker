﻿using FlowMaker.Middlewares;
using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Xml.Linq;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels
{
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
        public FlowMakerDebugViewModel(FlowManager flowManager, IFlowProvider flowProvider, IServiceProvider serviceProvider, IOptions<FlowMakerOption> options, IMessageBoxManager messageBoxManager)
        {
            this._flowManager = flowManager;
            this._flowProvider = flowProvider;
            this._serviceProvider = serviceProvider;
            this._messageBoxManager = messageBoxManager;
            this._flowMakerOption = options.Value;

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
                var step = Model.Steps.FirstOrDefault(v => v.Id == c.StepId);
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
                        flow.ErrorHandling = c.Context.ConfigDefinition.ErrorHandling;
                        Reset(flow.Steps);

                        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(id, "monitor");
                        if (mid is MonitorMiddleware monitor)
                        {
                            monitor.PercentChange.Subscribe(c =>
                            {
                                flow.Percent = c;
                            }).DisposeWith(flow.StepChange);
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
                            }).DisposeWith(flow.StepChange);
                        }
                    }
                    if (c.RunnerState == FlowState.Complete || c.RunnerState == FlowState.Cancel || c.RunnerState == FlowState.Error)
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

            async Task SetFlowStepAsync(IList<MonitorStepInfoViewModel> models, FlowDefinition flowDefinition)
            {
                foreach (var item in flowDefinition.Steps)
                {
                    if (!item.IsSubFlow)
                    {
                        Model.TotalCount++;
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


            await SetFlowStepAsync(Model.Steps, definition);

            foreach (var item in definition.Data)
            {
                if (item.IsInput)
                {
                    var data = new  SpikeInputViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
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
                    Model.ErrorHandling = config.ErrorHandling;
                    Model.LogView = config.LogView;
                    foreach (var item in Model.Data)
                    {
                        var data = config.Data.FirstOrDefault(c => c.Name == item.Name);
                        item.Value = data?.Value;
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
                     }).DisposeWith(Model.StepChange);
                    monitor.PercentChange.Subscribe(c =>
                    {
                        Model.Percent = c;
                    }).DisposeWith(Model.StepChange);
                }
            }

            await Activate();
        }

        public ReactiveCommand<Unit, Unit> RemoveCommand { get; }
        public async Task Remove()
        {
            if (Model is not null && Model.Id.HasValue)
            {
                await _flowManager.Stop(Model.Id.Value);
            }
            MessageBus.Current.SendMessage(this, "RemoveDebug");
        }

        public ReactiveCommand<Unit, Unit> RunCommand { get; }
        public async Task Run()
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

            Reset(monitorInfoViewModel.Steps);
            monitorInfoViewModel.StepChange?.Dispose();
            monitorInfoViewModel.StepChange = [];
            await _flowManager.Run(config, c =>
            {
                var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(c, "debug");
                if (mid is DebugMiddleware debug)
                {
                    debug.AddDebugs(c, monitorInfoViewModel.Steps.Where(c => c.IsDebug && c.Id.HasValue).Select(c => c.Id!.Value).ToList());
                }
                monitorInfoViewModel.Id = c;
                if (!string.IsNullOrWhiteSpace(monitorInfoViewModel.LogView))
                {
                    var vm = _serviceProvider.GetKeyedService<ILogInjectViewModel>(monitorInfoViewModel.LogView);
                    if (vm is ILogViewModel viewModel)
                    {
                        monitorInfoViewModel.DisplayView(viewModel);
                    }
                }
            });
        }
        protected void Reset(IList<MonitorStepInfoViewModel> steps)
        {
            foreach (var item in steps)
            {
                item.UsedTime = default;
                Reset(item.Steps);
            }
        }
        public ReactiveCommand<Unit, Unit> StopCommand { get; }
        public async Task Stop()
        {
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
        }


        public async ValueTask DisposeAsync()
        {
            if (Model is not null && Model.Id.HasValue)
            {
                await _flowManager.Stop(Model.Id.Value);
            }
        }
    }
}
