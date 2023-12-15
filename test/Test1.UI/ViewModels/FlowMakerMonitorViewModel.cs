using Accessibility;
using DynamicData;
using DynamicData.Binding;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.Services;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Test1.ViewModels
{
    public class FlowMakerMonitorViewModel : ViewModelBase
    {
        private readonly FlowManager _flowManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBoxManager _messageBoxManager;

        [Reactive]
        public int ColCount { get; set; } = 3;
        [Reactive]
        public int RowCount { get; set; } = 1;
        public const int MaxColCount = 4;
        public ObservableCollection<MonitorInfoViewModel> Flows { get; set; } = [];
        private readonly FlowMakerOption _flowMakerOption;
        //[Reactive]
        //public ObservableCollection<string> Categories { get; set; } = [];
        [Reactive]
        public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];

        public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, IMessageBoxManager messageBoxManager)
        {
            this._flowManager = flowManager;
            this._serviceProvider = serviceProvider;
            this._messageBoxManager = messageBoxManager;
            _flowMakerOption = options.Value;
            DeleteCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Delete);
            RunCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Run);
            StopCommand = ReactiveCommand.CreateFromTask<MonitorInfoViewModel>(Stop);
            LockCommand = ReactiveCommand.Create<MonitorInfoViewModel>(Lock);

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

            //var flows = Flows.ToObservableChangeSet();
            //flows.SubscribeMany(c =>
            //{
            //    return c.WhenValueChanged(v => v.Category, notifyOnInitialValue: false).WhereNotNull().Subscribe(v =>
            //    {
            //        c.Definitions.Clear();

            //        if (_flowMakerOption.Group.TryGetValue(v, out var group))
            //        {
            //            foreach (var item in group.StepDefinitions)
            //            {
            //                c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Step));
            //            }
            //        }

            //        foreach (var item in _flowManager.LoadFlows(v))
            //        {
            //            c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Flow));
            //        }
            //        foreach (var item in _flowManager.LoadConfigs(v))
            //        {
            //            c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Config));
            //        }

            //    });
            //}).Subscribe();

            //flows.SubscribeMany(c =>
            //{
            //    return c.WhenValueChanged(v => v.Definition, notifyOnInitialValue: false).WhereNotNull().Subscribe(async v =>
            //    {
            //        var stepDefinition = await _flowManager.GetStepDefinitionAsync(v.Category, v.Name);
            //        if (stepDefinition is null)
            //        {
            //            return;
            //        }
            //        c.Data.Clear();
            //        foreach (var item in stepDefinition.Data)
            //        {
            //            if (item.IsInput)
            //            {
            //                c.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
            //            }
            //        }
            //    });
            //}).Subscribe();
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
                flow.StepChange = _flowManager.GetStepChange(flow.Id.Value).Subscribe(c =>
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
                    var step = steps.FirstOrDefault(v => v.Id == c.Step.Id);
                    if (step is not null)
                    {
                        if (c.Status == StepState.Start)
                        {
                            step.Start();
                        }
                        if (c.Status == StepState.AllComplete)
                        {
                            step.Stop();
                        }
                    }
                });
            }

            var f = _flowManager.OnFlowChange.Synchronize().Subscribe(async c =>
            {
                if (c.FlowContext.FlowIds.Length > 1)
                {
                    return;
                }
                var flow = Flows.FirstOrDefault(v => v.Id == c.FlowContext.FlowIds[0]);

                if (c.RunnerState == RunnerState.Running)
                {
                    if (flow is null)
                    {
                        await Load(c.FlowContext.FlowDefinition.Category, c.FlowContext.FlowDefinition.Name, false, c.FlowContext.FlowIds[0]);
                    }
                    flow = Flows.First(v => v.Id == c.FlowContext.FlowIds[0]);
                    flow.Running = true;

                    if (flow is null || flow.StepChange is not null)
                    {
                        return;
                    }
                    flow.StepChange = _flowManager.GetStepChange(flow.Id.Value).Subscribe(c =>
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
                        var step = steps.FirstOrDefault(v => v.Id == c.Step.Id);
                        if (step is not null)
                        {
                            if (c.Status == StepState.Start)
                            {
                                step.Start();
                            }
                            if (c.Status == StepState.AllComplete)
                            {
                                step.Stop();
                            }
                        }
                    });
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
        /// <summary>
        /// 载入步骤或流程
        /// </summary>
        /// <param name="category"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public async Task Load(string category, string name, bool debug = true, Guid? id = null)
        {
            var stepDefinition = await _flowManager.GetStepDefinitionAsync(category, name);
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
                            var stepDefinition = await _flowManager.GetStepDefinitionAsync(item.Category, item.Name);
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
                        flow.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
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
                Name = monitorInfoViewModel.Name,
                FlowCategory = monitorInfoViewModel.Category,
                FlowName = monitorInfoViewModel.Name,
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
    }


    public class MonitorInfoViewModel(string category, string name, DefinitionType type) : ReactiveObject
    {
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
        public MonitorStepInfoViewModel()
        {
            ToggleDebugCommand = ReactiveCommand.Create(ToggleDebug);
        }
        [Reactive]
        public bool IsDebug { get; set; }
        public ReactiveCommand<Unit, Unit> ToggleDebugCommand { get; }
        public void ToggleDebug()
        {
            IsDebug = !IsDebug;
        }
        [Reactive]
        public TimeSpan? UsedTime { get; set; }
        public DateTime StartTime { get; set; }
        public IDisposable? Timer { get; set; }
        public void Start()
        {
            StartTime = DateTime.Now;
            Timer?.Dispose();
            Timer = Observable.Interval(TimeSpan.FromMilliseconds(20)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
                {
                    UsedTime = DateTime.Now - StartTime;
                });
        }
        public void Stop()
        {
            Timer?.Dispose();
            UsedTime = DateTime.Now - StartTime;
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
}