using DynamicData;
using DynamicData.Binding;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reactive;
using System.Reactive.Linq;
using Ty;
using Ty.Module.Configs;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public class FlowMakerEditViewModel : ViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    private readonly IMessageBoxManager _messageBoxManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFlowProvider _flowProvider;

    public FlowMakerEditViewModel(IOptions<FlowMakerOption> options, IMessageBoxManager messageBoxManager, IServiceProvider serviceProvider, IFlowProvider flowProvider)
    {
        _flowMakerOption = options.Value;
        CreateCommand = ReactiveCommand.CreateFromTask(Create);
        CreateEmbeddedSubStepCommand = ReactiveCommand.Create<FlowStepViewModel>(CreateEmbeddedSubStep);
        CreateGlobalDataCommand = ReactiveCommand.Create(CreateGlobalData);
        ChangeScaleCommand = ReactiveCommand.Create<int>(ChangeScale);
        ChangePreCommand = ReactiveCommand.Create<(FlowStepViewModel, IEnumerable<FlowStepViewModel>)>(ChangePre);
        UpActionCommand = ReactiveCommand.Create(UpAction);
        DownActionCommand = ReactiveCommand.Create(DownAction);
        DeleteActionCommand = ReactiveCommand.Create(DeleteAction);
        RemoveGlobalDataCommand = ReactiveCommand.Create<StepDataDefinitionViewModel>(RemoveGlobalData);
        EditGlobalDataOptionCommand = ReactiveCommand.Create<StepDataDefinitionViewModel>(EditGlobalDataOption);

        SaveCommand = ReactiveCommand.CreateFromTask(Save);

        AddWaitEventCommand = ReactiveCommand.Create(AddWaitEvent);
        RemoveWaitEventCommand = ReactiveCommand.Create<EventViewModel>(RemoveWaitEvent);

        ShowSubFlowCommand = ReactiveCommand.CreateFromTask<FlowStepViewModel>(ShowSubFlowAsync);

        InitConditionCommand = ReactiveCommand.Create(InitCondition);
        AddConfitionCommand = ReactiveCommand.Create(AddConfition);
        RemoveConditionCommand = ReactiveCommand.Create<FlowIfViewModel>(RemoveCondition);


        _messageBoxManager = messageBoxManager;
        _serviceProvider = serviceProvider;
        _flowProvider = flowProvider;
        GlobeDatas.ToObservableChangeSet().SubscribeMany(c =>
        {
            return c.WhenValueChanged(v => v.Type, notifyOnInitialValue: false).WhereNotNull().Throttle(TimeSpan.FromMilliseconds(200)).DistinctUntilChanged().ObserveOn(RxApp.MainThreadScheduler).Subscribe(v =>
            {
                if (_flowMakerOption.OptionProviders.TryGetValue(v, out var list))
                {
                    c.OptionProviders.Clear();
                    c.OptionProviders.Add(new NameValue("无", ""));
                    foreach (var item in list)
                    {
                        c.OptionProviders.Add(new NameValue(item.Name, item.Value));
                    }
                    var op = c.OptionProviders.FirstOrDefault(x => x.Value == c.OptionProviderName);
                    if (op is not null)
                    {
                        c.OptionProviderName = op.Value;
                    }
                }
            });
        }).Subscribe();
        this.WhenAnyValue(c => c.GanttMode).Skip(1).Subscribe(async c =>
        {
            if (!c)
            {
                var result = await _messageBoxManager.Conform.Handle(new ConformInfo("简单模式下，不支持并行执行，是否继续？") { OwnerTitle = WindowTitle });
                if (result)
                {
                    Render(Steps);
                }
                else
                {
                    Render(Steps);
                    GanttMode = true;
                }
            }
        });
        this.WhenAnyValue(c => c.Scale).Subscribe(c => Render(Steps));
        //this.WhenAnyValue(c => c.ShowEdit).Where(c => c).Subscribe(async c =>
        //{

        //});

        this.WhenAnyValue(c => c.FlowStep).WhereNotNull().Subscribe(async c =>
        {
            ShowEdit = c is not null;
            if (c is not null)
            {

                await SetStepGroup(c);
                await SetStepDefinitions(c);
            }
            ResetGlobeData();
        });


        foreach (var item in Enum.GetValues<FlowDataType>())
        {
            FlowDataTypes.Add(item);
        }
    }

    [Reactive]
    public ObservableCollection<FlowDataType> FlowDataTypes { get; set; } = [];

    [Reactive]
    public string? Category { get; set; }
    [Reactive]
    public string? Name { get; set; }

    public Guid? Id { get; set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public async Task Save()
    {
        if (string.IsNullOrEmpty(Category) || string.IsNullOrEmpty(Name))
        {
            return;
        }
        static FlowInput CreateInput(FlowStepInputViewModel flowStepInputViewModel)
        {
            FlowInput flowInput = new(!string.IsNullOrEmpty(flowStepInputViewModel.Name) ? flowStepInputViewModel.Name : flowStepInputViewModel.DisplayName, flowStepInputViewModel.Id)
            {
                Mode = flowStepInputViewModel.Mode,
                Value = flowStepInputViewModel.Value,
                Dims = flowStepInputViewModel.Dims.Select(c => c.Count).ToArray()
            };

            foreach (var subInput in flowStepInputViewModel.SubInputs)
            {
                flowInput.Inputs.Add(CreateInput(subInput));
            }
            return flowInput;
        }

        FlowDefinition flowDefinition = new()
        {
            Id = Id,
            Category = Category,
            Name = Name,
        };

        foreach (var item in GlobeDatas)
        {
            var data = new DataDefinition(item.Name ?? "", item.DisplayName ?? "", item.Type, item.DefaultValue)
            {
                IsInput = item.IsInput,
                IsOutput = item.IsOutput,
                IsArray = item.IsArray,
                Rank = item.Rank,
                FromStepId = item.FromStepId,
                FromStepPropName = item.FromStepPropName,
                OptionProviderName = item.OptionProviderName,
            };
            if (string.IsNullOrEmpty(data.OptionProviderName))
            {
                foreach (var option in item.Options)
                {
                    data.Options.Add(new OptionDefinition(option.DisplayName, option.Name));
                }
            }

            flowDefinition.Data.Add(data);
        }

        FlowStep CreateFlowStep(FlowStepViewModel item)
        {
            var f = new FlowStep()
            {
                Category = item.Category ?? string.Empty,
                DisplayName = item.DisplayName ?? string.Empty,
                Name = item.Name ?? string.Empty,
                Id = item.Id,
                SubFlowId = item.SubFlowId,
                Time = item.Time,
            };


            f.ErrorHandling = CreateInput(item.ErrorHandling);
            f.Repeat = CreateInput(item.Repeat);
            f.Retry = CreateInput(item.Retry);
            f.Timeout = CreateInput(item.TimeOut);
            f.Finally = CreateInput(item.Finally);
            foreach (var condition in item.Conditions)
            {
                f.Conditions.Add(new FlowCondition() { Name = condition.Name, Execute = condition.Execute, IsTrue = condition.IsTrue });
            }

            foreach (var wait in item.WaitEvents)
            {
                if (string.IsNullOrEmpty(wait.Name))
                {
                    continue;
                }
                f.WaitEvents.Add(new FlowEvent
                {
                    Type = EventType.Event,
                    EventName = wait.Name
                });
            }
            foreach (var preStep in item.PreSteps)
            {
                f.WaitEvents.Add(new FlowEvent
                {
                    Type = EventType.PreStep,
                    StepId = preStep
                });
            }

            foreach (var input in item.Inputs)
            {
                f.Inputs.Add(CreateInput(input));
            }
            foreach (var output in item.Outputs)
            {
                var nOutput = new FlowOutput
                {
                    GlobalDataName = output.GlobalDataName,
                    Name = output.Name,
                    Type = output.Type,
                    Mode = output.Mode,
                };

                f.Outputs.Add(nOutput);
            }
            return f;
        }

        foreach (var item in Steps)
        {
            var f = CreateFlowStep(item);

            flowDefinition.Steps.Add(f);
        }



        await _flowProvider.SaveFlow(flowDefinition);
        CloseModal(true);
    }
    public async Task Load(Guid? id)
    {
        Steps.Clear();
        GlobeDatas.Clear();
        Id = id;
        if (!id.HasValue)
        {
            return;
        }
        var flowDefinition = await _flowProvider.LoadFlowDefinitionAsync(id.Value);

        if (flowDefinition is null)
        {
            return;
        }


        Category = flowDefinition.Category;
        Name = flowDefinition.Name;

        foreach (var item in flowDefinition.Data)
        {
            var data = new StepDataDefinitionViewModel
            {
                IsInput = item.IsInput,
                IsOutput = item.IsOutput,
                IsArray = item.IsArray,
                Rank = item.Rank,
                FromStepId = item.FromStepId,
                FromStepPropName = item.FromStepPropName,
                DefaultValue = item.DefaultValue,
                Name = item.Name,
                DisplayName = item.DisplayName,
                IsStepOutput = item.FromStepId.HasValue,
                OptionProviderName = item.OptionProviderName,
            };
            foreach (var option in item.Options)
            {
                data.Options.Add(new FlowStepOptionViewModel(option.DisplayName, option.Name));
            }
            GlobeDatas.Add(data);
            data.Type = item.Type;//data添加到GlobeDatas后,再赋值Type可以初始化选项集
        }

        foreach (var item in flowDefinition.Steps)
        {
            var flowStepViewModel = await CreateStepViewModelAsync(item);

            Steps.Add(flowStepViewModel);
        }


        Render(Steps);
    }

    private async Task<FlowStepViewModel> CreateStepViewModelAsync(FlowStep flowStepItem)
    {
        FlowStepViewModel flowStepViewModel = new(this)
        {
            DisplayName = flowStepItem.DisplayName,
            Id = flowStepItem.Id,
            Status = StepStatus.Normal,
            Time = flowStepItem.Time,
            IsSubFlow = flowStepItem.SubFlowId.HasValue,
            SubFlowId = flowStepItem.SubFlowId,
            Category = flowStepItem.Category,
            Name = flowStepItem.Name
        };

        if (flowStepItem.SubFlowId.HasValue)
        {
            //var flowDefinition = await _flowProvider.LoadFlowDefinitionAsync(flowStepItem.SubFlowId.Value);

            //if (flowDefinition is not null)
            //{
            //    foreach (var item2 in flowDefinition.Steps)
            //    {
            //        flowStepViewModel.Steps.Add(await CreateStepViewModelAsync(item2));
            //    }
            //}
        }
        else
        {
            await flowStepViewModel.SetInputOutputs(flowStepItem);
        }



        foreach (var wait in flowStepItem.WaitEvents)
        {
            switch (wait.Type)
            {
                case EventType.PreStep when wait.StepId.HasValue:
                    flowStepViewModel.PreSteps.Add(wait.StepId.Value);
                    break;
                case EventType.Event when !string.IsNullOrEmpty(wait.EventName):
                    flowStepViewModel.WaitEvents.Add(new EventViewModel { Name = wait.EventName });
                    break;
                case EventType.StartFlow:
                    break;
                default:
                    break;
            }
        }

        foreach (var item in flowStepItem.Conditions)
        {
            var entity = flowStepViewModel.Conditions.FirstOrDefault(c => c.Name == item.Name);
            if (entity is not null)
            {
                entity.Execute = item.Execute;
                entity.IsTrue = item.IsTrue;
            }
            else
            {
                var data = GlobeDatas.FirstOrDefault(c => c.Name == item.Name);
                flowStepViewModel.Conditions.Add(new FlowIfViewModel { Name = item.Name, IsTrue = item.IsTrue, Execute = item.Execute, DisplayName = data?.DisplayName });
            }
        }


        return flowStepViewModel;
    }

    #region Steps



    [Reactive]
    public bool ShowEdit { get; set; }
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<FlowStepViewModel, Unit> CreateEmbeddedSubStepCommand { get; }
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = [];
    public async Task Create()
    {
        var model = new FlowStepViewModel(this);

        if (FlowStep is not null)
        {
            var steps = GetParent(FlowStep, Steps);
            if (steps is null)
            {
                return;
            }
            if (GanttMode)
            {
                model.PreSteps.Add(FlowStep.Id);
            }
            steps.Insert(steps.IndexOf(FlowStep) + 1, model);
            var currentSteps = CurrentSteps;
            ChangePre((FlowStep, CurrentSteps));
            ChangePre((model, currentSteps));
        }
        else
        {
            Steps.Add(model);
            ChangePre((model, Steps));
        }
        Render(Steps);

        await Task.CompletedTask;
    }


    public void CreateEmbeddedSubStep(FlowStepViewModel flowStepViewModel)
    {
        if (flowStepViewModel is not null && flowStepViewModel.IsSubFlow)
        {
            var model = new FlowStepViewModel(this);

            flowStepViewModel.Steps.Add(model);
        }
    }
    private static ObservableCollection<FlowStepViewModel>? GetParent(FlowStepViewModel flowStepViewModel, ObservableCollection<FlowStepViewModel> all)
    {
        //从steps中找到这个元素,那么返回steps,如果找不到,那么递归找
        var parent = all.Contains(flowStepViewModel);
        if (parent)
        {
            return all;
        }
        foreach (var item in all)
        {
            var r = GetParent(flowStepViewModel, item.Steps);
            if (r is not null)
            {
                return r;
            }
        }
        return null;

    }

    public ReactiveCommand<Unit, Unit> DeleteActionCommand { get; }
    public void DeleteAction()
    {
        if (FlowStep is null)
        {
            return;
        }

        var temp = FlowStep;
        ChangePre((FlowStep, CurrentSteps));
        var deletes = GlobeDatas.Where(c => c.FromStepId == temp.Id);

        GlobeDatas.RemoveMany(deletes);
        var steps = GetParent(temp, Steps);
        steps?.Remove(temp);
        Render(Steps);
    }
    [Reactive]
    public FlowStepViewModel? FlowStep { get; set; }
    /// <summary>
    /// 当前步骤, 相同的集合
    /// </summary>
    public IEnumerable<FlowStepViewModel>? CurrentSteps { get; set; }
    /// <summary>
    /// 编辑依赖
    /// </summary>
    [Reactive]
    public bool EditPreStep { get; set; }
    /// <summary>
    /// 修改依赖
    /// </summary>
    public ReactiveCommand<(FlowStepViewModel, IEnumerable<FlowStepViewModel>), Unit> ChangePreCommand { get; }
    public void ChangePre((FlowStepViewModel, IEnumerable<FlowStepViewModel>) model)
    {
        var c = model.Item1;
        if (FlowStep is null)//全部未选中
        {
            FlowStep = c;
            CurrentSteps = model.Item2;
            FlowStep.Status = StepStatus.Selected;
            RenderPreStep(c);
            ShowEdit = true;
        }
        else//有选中
        {
            if (FlowStep == c)//重新点击自己
            {
                ResetGlobeData();
                FlowStep = null;
                if (CurrentSteps is not null)
                {
                    foreach (var item in CurrentSteps)
                    {
                        item.Status = StepStatus.Normal;
                    }
                }

                ShowEdit = false;
                EditPreStep = false;
            }
            else
            {
                if (!GanttMode || !EditPreStep)
                {
                    if (CurrentSteps is not null)
                    {
                        foreach (var item in CurrentSteps)
                        {
                            item.Status = StepStatus.Normal;
                        }
                    }
                    FlowStep = c;
                    FlowStep.Status = StepStatus.Selected;
                    RenderPreStep(c);
                    return;
                }

                if (!FlowStep.PreSteps.Contains(c.Id))
                {
                    if (!Verification(c, FlowStep))
                    {
                        return;
                    }

                    FlowStep.PreSteps.Add(c.Id);
                    RenderPreStep(FlowStep);
                }
                else
                {
                    c.Status = StepStatus.Normal;
                    FlowStep.PreSteps.Remove(c.Id);
                }
            }
        }
        if (CurrentSteps is not null)
        {
            Render(CurrentSteps);
        }
    }
    public void ResetGlobeData()
    {
        if (FlowStep is null)
        {
            return;
        }
        foreach (var item in FlowStep.Outputs)
        {
            if (item.Mode != OutputMode.Drop)
            {
                var output = GlobeDatas.FirstOrDefault(c => c.FromStepId == FlowStep.Id && c.FromStepPropName == item.Name);
                if (output is null)
                {
                    GlobeDatas.Add(new StepDataDefinitionViewModel
                    {
                        Name = item.GlobalDataName,
                        DisplayName = item.GlobalDataName,
                        FromStepPropName = item.Name,
                        FromStepId = FlowStep.Id,
                        IsStepOutput = true,
                        Type = item.Type,
                    });
                }
                else
                {
                    output.Name = item.GlobalDataName;
                    if (string.IsNullOrEmpty(output.DisplayName))
                    {
                        output.DisplayName = item.GlobalDataName;
                    }
                }
            }
            else
            {
                var output = GlobeDatas.FirstOrDefault(c => c.FromStepId == FlowStep.Id && c.FromStepPropName == item.Name);
                if (output is not null)
                {
                    GlobeDatas.Remove(output);
                }
            }
        }
    }
    public void RenderPreStep(FlowStepViewModel step, bool first = true)
    {
        foreach (var item in step.PreSteps)
        {
            var action = CurrentSteps?.FirstOrDefault(v => v.Id == item);
            if (action is not null)
            {
                if (first || action.Status == StepStatus.PreStep)
                {
                    action.Status = StepStatus.PreStep;
                }
                else
                {
                    action.Status = StepStatus.IndirectPreStep;
                }
                RenderPreStep(action, false);
            }
        }
    }
    public ReactiveCommand<Unit, Unit> UpActionCommand { get; }
    public ReactiveCommand<Unit, Unit> DownActionCommand { get; }
    public void UpAction()
    {
        if (FlowStep is null)
        {
            return;
        }
        MoveAction(FlowStep, true);
    }
    public void DownAction()
    {
        if (FlowStep is null)
        {
            return;
        }
        MoveAction(FlowStep, false);
    }
    public void MoveAction(FlowStepViewModel action, bool up)
    {
        var steps = GetParent(action, Steps);
        if (steps is null)
        {
            return;
        }
        var oldIndex = steps.IndexOf(action);
        if (oldIndex >= 0)
        {
            if (up && oldIndex != 0 || !up && oldIndex != steps.Count - 1)
            {
                steps.Move(oldIndex, up ? --oldIndex : ++oldIndex);
                Render(Steps);
                return;
            }
        }
    }
    /// <summary>
    /// 显示比例
    /// </summary>
    [Reactive]
    public int Scale { get; set; } = 40;
    public ReactiveCommand<int, Unit> ChangeScaleCommand { get; }
    public void ChangeScale(int d)
    {
        Scale += d;
        if (Scale < 0)
        {
            Scale = 0;
        }
        if (Scale > 10000)
        {
            Scale = 10000;
        }
    }

    [Reactive]
    public ObservableCollection<TimeSpan> DateAxis { get; set; } = [];


    //计算嵌入步骤的时间
    public TimeSpan GetEmbeddedTime(FlowStepViewModel flowStepViewModel)
    {
        TimeSpan time = TimeSpan.FromSeconds(0);
        foreach (var item in flowStepViewModel.Steps)
        {
            if (item.IsSubFlow)
            {
                time += GetEmbeddedTime(item);
            }
            else
            {
                time += item.Time;
            }
        }
        return time;
    }


    /// <summary>
    /// 渲染
    /// </summary>
    public void Render(IEnumerable<FlowStepViewModel> steps)
    {
        RenderSimpleMode();
        var newList = Order(steps);
        newList.Reverse();
        TimeSpan max = TimeSpan.FromSeconds(0);
        foreach (var item in newList)
        {
            if (item.IsSubFlow)
            {
                Render(item.Steps);
                item.Time = TimeSpan.FromSeconds(item.Steps.Sum(c => c.Time.TotalSeconds));
            }
            if (item.PreSteps.Count != 0)
            {
                TimeSpan maxSpan = TimeSpan.FromSeconds(0);
                for (int i = 0; i < item.PreSteps.Count; i++)
                {
                    var preaction = item.PreSteps[i];
                    var action = Steps.FirstOrDefault(c => c.Id == preaction);
                    if (action is null)
                    {
                        item.PreSteps.Remove(preaction);
                        continue;
                    }


                    var span = action.Time + action.PreTime;
                    if (maxSpan < span)
                    {
                        maxSpan = span;
                    }
                }

                item.PreTime = maxSpan;
                if (max < maxSpan + item.Time)
                {
                    max = maxSpan + item.Time;
                }
            }
            else
            {
                item.PreTime = TimeSpan.FromSeconds(0);
                if (max < item.Time)
                {
                    max = item.Time;
                }
            }
            //if (!GanttMode)
            //{
            //    item.PreTime = TimeSpan.FromSeconds(0);
            //}
        }
        if (steps == Steps)
        {
            DateAxis.Clear();
            if (max.TotalSeconds > 0)
            {
                var s = 100 / Scale;
                var count = max.TotalSeconds / s;
                if (max.TotalSeconds % s > 0)
                {
                    count++;
                }
                for (int i = 0; i <= count; i++)
                {
                    DateAxis.Add(new TimeSpan(0, 0, i * s));
                }
            }
        }

    }
    /// <summary>
    /// 验证是否有循环依赖
    /// </summary>
    /// <param name="newPre"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public bool Verification(FlowStepViewModel newPre, FlowStepViewModel source)
    {
        if (newPre.PreSteps.Any(c => c == source.Id))
        {
            newPre.Status = StepStatus.DependencyError;
            return false;
        }

        foreach (var item in newPre.PreSteps)
        {
            var action = Steps.First(c => c.Id == item);

            var r = Verification(action, source);
            if (!r)
            {
                action.Status = StepStatus.DependencyError;
                return false;
            }
        }

        return true;
    }
    /// <summary>
    /// 依赖关系算法,根据依赖关系，把没有依赖的放到前面。
    /// https://blog.csdn.net/qq_33426531/article/details/120565539
    /// </summary>
    /// <param name="ganttActions"></param>
    /// <returns></returns>
    public static List<FlowStepViewModel> Order(IEnumerable<FlowStepViewModel> ganttActions)
    {
        List<FlowStepViewModel> result = [];

        List<Guid> total = ganttActions.Select(c => c.Id).ToList();

        List<(Guid, Guid)> temp = [];

        foreach (var action in ganttActions)
        {
            foreach (var item in action.PreSteps)
            {
                temp.Add((action.Id, item));
            }
        }

        while (total.Count != 0)
        {
            var has = temp.Select(c => c.Item2).Distinct().ToList();
            var orderd = total.Except(has);
            total = has;
            temp = temp.Where(c => !orderd.Contains(c.Item1)).ToList();
            result.AddRange(ganttActions.Where(c => orderd.Contains(c.Id)).ToList());
        }

        return result;
    }

    #endregion

    #region Wait
    [Reactive]
    public ObservableCollection<string> StepGroups { get; set; } = [];
    [Reactive]
    public ObservableCollection<NameValue<Guid?>> StepDefinitions { get; set; } = [];

    public ReactiveCommand<Unit, Unit> AddWaitEventCommand { get; }
    public void AddWaitEvent()
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.WaitEvents.Add(new EventViewModel());
    }
    public ReactiveCommand<EventViewModel, Unit> RemoveWaitEventCommand { get; }
    public void RemoveWaitEvent(EventViewModel eventName)
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.WaitEvents.Remove(eventName);
    }

    #endregion

    #region Condition
    public StepDataDefinitionViewModel? SelectConditionData { get; set; }
    public ObservableCollection<StepDataDefinitionViewModel> ConditionDatas { get; set; } = [];
    public ReactiveCommand<Unit, Unit> InitConditionCommand { get; set; }

    public void InitCondition()
    {
        ConditionDatas.Clear();
        foreach (var item in GlobeDatas)
        {
            if (item.Type == FlowDataType.Boolean)
            {
                ConditionDatas.Add(item);
            }
        }
        if (FlowStep is null)
        {
            return;
        }
        foreach (var item in FlowStep.Conditions)
        {
            var data = GlobeDatas.FirstOrDefault(c => c.Name == item.Name);
            item.DisplayName = data?.DisplayName;
        }
    }

    public ReactiveCommand<Unit, Unit> AddConfitionCommand { get; set; }
    public void AddConfition()
    {
        if (FlowStep is null || SelectConditionData is null || string.IsNullOrWhiteSpace(SelectConditionData.Name))
        {
            return;
        }
        FlowStep.Conditions.Add(new FlowIfViewModel() { Name = SelectConditionData.Name, DisplayName = SelectConditionData.DisplayName, Execute = true, IsTrue = true });
    }
    public ReactiveCommand<FlowIfViewModel, Unit> RemoveConditionCommand { get; }
    public void RemoveCondition(FlowIfViewModel condition)
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.Conditions.Remove(condition);
    }

    #endregion



    #region GlobalDatas
    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = [];

    public ReactiveCommand<Unit, Unit> CreateGlobalDataCommand { get; }
    public void CreateGlobalData()
    {
        GlobeDatas.Add(new StepDataDefinitionViewModel());
    }

    public ReactiveCommand<StepDataDefinitionViewModel, Unit> RemoveGlobalDataCommand { get; }
    public void RemoveGlobalData(StepDataDefinitionViewModel stepDataDefinitionViewModel)
    {
        GlobeDatas.Remove(stepDataDefinitionViewModel);
    }
    [Reactive]
    public StepDataDefinitionViewModel? CurrentGlobalData { get; set; }
    [Reactive]
    public bool ShowGlobalDataOption { get; set; }
    public ReactiveCommand<StepDataDefinitionViewModel, Unit> EditGlobalDataOptionCommand { get; }
    public void EditGlobalDataOption(StepDataDefinitionViewModel stepDataDefinitionViewModel)
    {
        CurrentGlobalData = stepDataDefinitionViewModel;
        ShowGlobalDataOption = true;
    }

    #endregion

    #region Input
    public ReactiveCommand<FlowStepViewModel, Unit> ShowSubFlowCommand { get; }
    public async Task ShowSubFlowAsync(FlowStepViewModel flowStepViewModel)
    {
        var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
        await vm.Load(flowStepViewModel.SubFlowId);
        _messageBoxManager.Modals.Handle(new Ty.Services.ModalInfo("牛马编辑器:" + flowStepViewModel.Name, vm) { OwnerTitle = WindowTitle }).Subscribe();
    }
    public async Task SetStepGroup(FlowStepViewModel flowStepViewModel)
    {
        List<string> keys = [];
        StepGroups.Clear();
        if (!flowStepViewModel.IsSubFlow)
        {
            keys.AddRange(_flowMakerOption.Group.Keys);
        }
        else
        {
            keys.AddRange(await _flowProvider.LoadCategories());
        }
        foreach (var item in keys)
        {
            if (!StepGroups.Contains(item))
            {
                StepGroups.Add(item);
            }
        }
    }
    public async Task SetStepDefinitions(FlowStepViewModel flowStepViewModel)
    {
        StepDefinitions.Clear();

        if (string.IsNullOrEmpty(flowStepViewModel.Category))
        {
            return;
        }
        if (_flowMakerOption.Group.TryGetValue(flowStepViewModel.Category, out var group))
        {
            foreach (var item in group.StepDefinitions)
            {
                StepDefinitions.Add(new NameValue<Guid?>(item.Name, null));
            }
            if (!string.IsNullOrEmpty(flowStepViewModel.Name))
            {
                flowStepViewModel.SelectedStep = StepDefinitions.FirstOrDefault(c => c.Name == flowStepViewModel.Name);
            }
        }
        else
        {
            foreach (var item in await _flowProvider.LoadFlowNamesByCategory(flowStepViewModel.Category))
            {
                StepDefinitions.Add(new NameValue<Guid?>(item.Name, item.Id));
            }

            if (flowStepViewModel.SubFlowId.HasValue)
            {
                flowStepViewModel.SelectedStep = StepDefinitions.FirstOrDefault(c => c.Value == flowStepViewModel.SubFlowId);
            }
        }
    }
    public async Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name)
    {
        return await _flowProvider.GetStepDefinitionAsync(category, name);
    }




    public void InitGlobal(FlowStepInputViewModel flowStepInputViewModel)
    {
        flowStepInputViewModel.GlobalDataDefinitions.Clear();

        foreach (var item in GlobeDatas)
        {
            if (item.Type == flowStepInputViewModel.Type)
            {
                flowStepInputViewModel.GlobalDataDefinitions.Add(item);
            }
        }
        flowStepInputViewModel.HasGlobe = flowStepInputViewModel.GlobalDataDefinitions.Any();
    }


    public void InsertConverterInput(FlowStepInputViewModel flowStepInputViewModel, FlowInput? flowInput = null)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
        if (flowInput is not null)
        {
            flowStepInputViewModel.Mode = flowInput.Mode;
            flowStepInputViewModel.Id = flowInput.Id;
            flowStepInputViewModel.Value = flowInput.Value;
        }

        if (flowStepInputViewModel.Mode == InputMode.Array)
        {
            if (flowInput is not null)
            {
                for (int i = 0; i < flowInput.Dims.Length; i++)
                {
                    if (flowStepInputViewModel.Dims.Count > i)
                    {
                        flowStepInputViewModel.Dims[i].Count = flowInput.Dims[i];
                    }
                }

            }
            int count = flowStepInputViewModel.Dims.Select(c => c.Count).Aggregate((a, b) => a * b);
            List<string> newDisplayNames = [];

            for (int i = 0; i < count; i++)
            {
                int index = i;  // 一维索引

                int[] indices = new int[flowStepInputViewModel.Dims.Count];

                for (int j = flowStepInputViewModel.Dims.Count - 1; j >= 0; j--)
                {
                    indices[j] = (index % flowStepInputViewModel.Dims[j].Count) + 1;
                    index /= flowStepInputViewModel.Dims[j].Count;
                }
                var displayName = string.Join(",", indices);
                if (flowInput is not null)
                {
                    var input = new FlowStepInputViewModel(displayName, displayName, flowStepInputViewModel.Type, 0, this);

                    var sub = flowInput.Inputs.FirstOrDefault(c => c.Name == displayName);
                    if (sub is not null)
                    {
                        input.Mode = sub.Mode;
                        input.Id = sub.Id;
                        input.Value = sub.Value;

                        InsertConverterInput(input, sub);
                    }
                    flowStepInputViewModel.SubInputs.Add(input);
                }
                newDisplayNames.Add(displayName);

                if (!flowStepInputViewModel.SubInputs.Any(s => s.DisplayName == displayName))
                {
                    var input = new FlowStepInputViewModel(displayName, displayName, flowStepInputViewModel.Type, 0, this);
                    if (flowStepInputViewModel.Options.Count != 0)
                    {
                        input.HasOption = true;
                        foreach (var item2 in flowStepInputViewModel.Options)
                        {
                            input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                        }
                    }
                    flowStepInputViewModel.SubInputs.Add(input);
                }
            }

            var itemsToRemove = flowStepInputViewModel.SubInputs.Where(s => !newDisplayNames.Contains(s.DisplayName)).ToList();
            foreach (var item in itemsToRemove)
            {
                flowStepInputViewModel.SubInputs.Remove(item);
            }

            var sortedSubInputs = flowStepInputViewModel.SubInputs.OrderBy(s => s.DisplayName).ToList();
            flowStepInputViewModel.SubInputs.Clear();
            foreach (var item in sortedSubInputs)
            {
                flowStepInputViewModel.SubInputs.Add(item);
            }
        }
    }

    public async Task InitOptions(FlowStepInputViewModel flowStepInputViewModel, DataDefinition stepDataDefinition)
    {
        if (!string.IsNullOrEmpty(stepDataDefinition.OptionProviderName))
        {
            var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(stepDataDefinition.OptionProviderName);
            if (pp is null)
            {
                return;
            }

            flowStepInputViewModel.Options.Clear();
            await foreach (var item in pp.GetOptions())
            {
                flowStepInputViewModel.Options.Add(new FlowStepOptionViewModel(item.Name, item.Value));
            }


        }
    }
    #endregion


    #region SimpleMode
    [Reactive]
    public bool GanttMode { get; set; } = false;

    public void RenderSimpleMode()
    {
        //if (GanttMode)
        //{
        //    return;
        //}
        ////遍历Steps，第0项没有prestep,其他项的prestep都是自己的前一项
        //for (int i = 0; i < Steps.Count; i++)
        //{
        //    var item = Steps[i];
        //    if (i == 0)
        //    {
        //        item.PreSteps.Clear();
        //    }
        //    else
        //    {
        //        item.PreSteps.Clear();
        //        item.PreSteps.Add(Steps[i - 1].Id);
        //    }
        //}
    }

    #endregion
}

public class StepDataDefinitionViewModel : ReactiveObject
{
    public StepDataDefinitionViewModel()
    {
        AddOptionCommand = ReactiveCommand.Create(AddOption);
        RemoveOptionCommand = ReactiveCommand.Create<FlowStepOptionViewModel>(RemoveOption);
    }
    [Reactive]
    public FlowDataType Type { get; set; }
    [Reactive]
    public string? Name { get; set; }
    [Reactive]
    public string? DisplayName { get; set; }
    [Reactive]
    public string? DefaultValue { get; set; }

    [Reactive]
    public bool IsInput { get; set; }
    [Reactive]
    public bool IsOutput { get; set; }
    [Reactive]
    public bool IsArray { get; set; }
    [Reactive]
    public int Rank { get; set; }
    [Reactive]
    public bool IsStepOutput { get; set; }


    [Reactive]
    public string? FromStepPropName { get; set; }
    public Guid? FromStepId { get; set; }

    [Reactive]
    public string? OptionProviderName { get; set; }

    public ReactiveCommand<Unit, Unit> AddOptionCommand { get; }
    public void AddOption()
    {
        Options.Add(new FlowStepOptionViewModel("", ""));
    }
    public ReactiveCommand<FlowStepOptionViewModel, Unit> RemoveOptionCommand { get; }
    public void RemoveOption(FlowStepOptionViewModel flowStepOptionViewModel)
    {
        Options.Remove(flowStepOptionViewModel);
    }

    [Reactive]
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];
    [Reactive]
    public ObservableCollection<NameValue> OptionProviders { get; set; } = [];
}

public class FlowIfViewModel : ReactiveObject
{
    [Reactive]
    public string? DisplayName { get; set; }
    [Reactive]
    public required string Name { get; set; }
    [Reactive]
    public bool Execute { get; set; }
    [Reactive]
    public bool IsTrue { get; set; }
}
public class FlowStepInputViewModel : ReactiveObject
{
    private readonly FlowMakerEditViewModel _flowStepEditViewModel;

    [Reactive]
    public Guid Id { get; set; }
    public FlowStepInputViewModel(string name, string displayName, FlowDataType type, int rank, FlowMakerEditViewModel flowStepEditViewModel)
    {
        Id = Guid.NewGuid();
        Name = name;
        DisplayName = displayName;
        Type = type;
        _flowStepEditViewModel = flowStepEditViewModel;
        ChangeModeCommand = ReactiveCommand.Create(ChangeMode);
        Rank = rank;
        for (int i = 0; i < rank; i++)
        {
            Dims.Add(new DimViewModel() { Name = (i + 1).ToString() });
        }

        this.WhenAnyValue(c => c.Type).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            _flowStepEditViewModel.InitGlobal(this);
        });


        this.WhenAnyValue(c => c.Mode).DistinctUntilChanged().Subscribe(c =>
        {
            if (c == InputMode.Array)
            {
                SubInputs.Clear();
            }

            _flowStepEditViewModel.InsertConverterInput(this);
            _flowStepEditViewModel.InitGlobal(this);
        });
        Dims.ToObservableChangeSet().SubscribeMany(c =>
        {
            return c.WhenValueChanged(v => v.Count, notifyOnInitialValue: false).WhereNotNull().Throttle(TimeSpan.FromMilliseconds(200)).DistinctUntilChanged().ObserveOn(RxApp.MainThreadScheduler).Subscribe(v =>
            {
                _flowStepEditViewModel.InsertConverterInput(this);
            });
        }).Subscribe();

        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            ModelName = c switch
            {
                InputMode.Normal => "普通",
                InputMode.Option => "选项",
                InputMode.Global => "全局变量",
                InputMode.Array => "数组",
                InputMode.Event => "事件",
                _ => null
            };
        });

    }



    [Reactive]
    public string Name { get; set; }

    /// <summary>
    /// 使用选项
    /// </summary>
    [Reactive]
    public InputMode Mode { get; set; }

    [Reactive]
    public bool HasOption { get; set; }

    [Reactive]
    public bool HasGlobe { get; set; }

    [Reactive]
    public bool IsArray { get; set; }

    public ReactiveCommand<Unit, Unit> ChangeModeCommand { get; }

    [Reactive]
    public string? ModelName { get; set; }
    public void ChangeMode()
    {
        //每次进入时都改变模式
        List<InputMode> modes = [];
        if (HasOption)
        {
            modes.Add(InputMode.Option);
        }
        modes.Add(InputMode.Normal);
        if (HasGlobe)
        {
            modes.Add(InputMode.Global);
        }
        if (IsArray)
        {
            modes.Add(InputMode.Array);
        }
        modes.Add(InputMode.Event);

        //从当前模式开始，找到下一个模式
        var index = modes.IndexOf(Mode);
        if (index == -1)
        {
            Mode = modes[0];
        }
        else
        {
            if (index == modes.Count - 1)
            {
                Mode = modes[0];
            }
            else
            {
                Mode = modes[index + 1];
            }
        }
    }


    /// <summary>
    /// 显示名称，描述
    /// </summary>
    [Reactive]
    public string DisplayName { get; set; }
    [Reactive]
    public FlowDataType Type { get; set; }
    [Reactive]
    public string? Value { get; set; }
    [Reactive]
    public int Rank { get; set; }
    [Reactive]
    public ObservableCollection<DimViewModel> Dims { get; set; } = [];

    [Reactive]
    public bool Disable { get; set; }

    public void SetDisable()
    {
        Value = null;
        Disable = true;
        Mode = InputMode.Normal;
    }

    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> SubInputs { get; set; } = [];


    [Reactive]
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];

    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobalDataDefinitions { get; set; } = [];
}
public class DimViewModel : ReactiveObject
{
    [Reactive]
    public required string Name { get; set; }
    [Reactive]
    public int Count { get; set; }
}
public class FlowStepOptionViewModel(string displayName, string name) : ReactiveObject
{
    [Reactive]
    public string Name { get; set; } = name;
    [Reactive]
    public string DisplayName { get; set; } = displayName;
}
public class FlowStepOutputViewModel : ReactiveObject
{

    public FlowStepOutputViewModel(string name, string displayName, FlowDataType type)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;


        ChangeModeCommand = ReactiveCommand.Create(ChangeMode);
        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            ModelName = c switch
            {
                OutputMode.Drop => "丢弃",
                OutputMode.Global => "全局变量",
                _ => null
            };
        });
    }

    [Reactive]
    public string Name { get; set; }
    [Reactive]
    public OutputMode Mode { get; set; }

    public ReactiveCommand<Unit, Unit> ChangeModeCommand { get; }

    [Reactive]
    public string? ModelName { get; set; }
    public void ChangeMode()
    {
        //每次进入时都改变模式
        List<OutputMode> modes = [OutputMode.Drop, OutputMode.Global];

        //从当前模式开始，找到下一个模式
        var index = modes.IndexOf(Mode);
        if (index == -1)
        {
            Mode = modes[0];
        }
        else
        {
            if (index == modes.Count - 1)
            {
                Mode = modes[0];
            }
            else
            {
                Mode = modes[index + 1];
            }
        }
    }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    [Reactive]
    public string DisplayName { get; set; }


    public FlowDataType Type { get; set; }


    public string? GlobalDataName { get; set; }

}
[DebuggerDisplay("{Name}:{Id}")]
public class FlowStepViewModel : ReactiveObject
{
    private readonly FlowMakerEditViewModel _flowMakerEditViewModel;

    public FlowMakerEditViewModel MainViewModel => _flowMakerEditViewModel;
    public FlowStepViewModel(FlowMakerEditViewModel flowMakerEditViewModel)
    {
        Id = Guid.NewGuid();

        _flowMakerEditViewModel = flowMakerEditViewModel;
        ErrorHandling = new FlowStepInputViewModel("ErrorHandling", "错误处理", FlowDataType.Number, 0, _flowMakerEditViewModel)
        {
            Value = "Skip",
            Id = Guid.NewGuid(),
            Mode = InputMode.Option,
            Options = [new FlowStepOptionViewModel("跳过", "0"), new FlowStepOptionViewModel("停止", "1"), new FlowStepOptionViewModel("立即停止", "2")],
            HasOption = true,
        };
        Repeat = new FlowStepInputViewModel("Repeat", "重复", FlowDataType.Number, 0, _flowMakerEditViewModel)
        {
            Value = "1",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Retry = new FlowStepInputViewModel("Retry", "重试", FlowDataType.Number, 0, _flowMakerEditViewModel)
        {
            Value = "0",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        TimeOut = new FlowStepInputViewModel("TimeOut", "超时", FlowDataType.Number, 0, _flowMakerEditViewModel)
        {
            Value = "0",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Finally = new FlowStepInputViewModel("Finally", "总会执行", FlowDataType.Boolean, 0, _flowMakerEditViewModel)
        {
            Value = "false",
            Mode = InputMode.Option,
            Id = Guid.NewGuid(),
            Options = [new FlowStepOptionViewModel("是", "true"), new FlowStepOptionViewModel("否", "false")],
            HasOption = true,
        };
        this.WhenAnyValue(c => c.Category).Skip(1).WhereNotNull().DistinctUntilChanged().Subscribe(async c =>
        {
            await _flowMakerEditViewModel.SetStepDefinitions(this);
        });
        this.WhenAnyValue(c => c.IsSubFlow).Skip(1).Subscribe(async c =>
        {
            await _flowMakerEditViewModel.SetStepGroup(this);
            await SetInputOutputs();
        });

        this.WhenAnyValue(c => c.ShowSubSteps).Skip(1).Subscribe(async c =>
        {
        
        });


        this.WhenAnyValue(c => c.SelectedStep).Skip(1).WhereNotNull().DistinctUntilChanged().Subscribe(async c =>
        {
            Name = c.Name;
            SubFlowId = c.Value;

            if (SubFlowId.HasValue)
            {

            }

            await SetInputOutputs();
        });
    }
    [Reactive]
    public NameValue<Guid?>? SelectedStep { get; set; }
    [Reactive]
    public bool IsSubFlow { get; set; }
    [Reactive]
    public Guid? SubFlowId { get; set; }
    [Reactive]
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = [];


    public async Task SetInputOutputs(FlowStep? flowStep = null)
    {
        Outputs.Clear();
        Inputs.Clear();

        if (flowStep is not null)
        {
            _flowMakerEditViewModel.InsertConverterInput(ErrorHandling, flowStep.ErrorHandling);
            _flowMakerEditViewModel.InsertConverterInput(Repeat, flowStep.Repeat);
            _flowMakerEditViewModel.InsertConverterInput(Retry, flowStep.Retry);
            _flowMakerEditViewModel.InsertConverterInput(TimeOut, flowStep.Timeout);
            _flowMakerEditViewModel.InsertConverterInput(Finally, flowStep.Finally);
        }

        if (!string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(Name))
        {
            var stepDef = await _flowMakerEditViewModel.GetStepDefinitionAsync(Category, Name);
            if (stepDef is not null)
            {
                foreach (var item in stepDef.Data)
                {
                    if (item.IsInput)
                    {
                        var flowInput = flowStep?.Inputs.FirstOrDefault(c => c.Name == item.Name);

                        var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, item.Rank, _flowMakerEditViewModel);
                        input.IsArray = item.IsArray;

                        await _flowMakerEditViewModel.InitOptions(input, item);
                        foreach (var item2 in item.Options)
                        {
                            input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                        }
                        if (input.Options.Count != 0)
                        {
                            input.HasOption = true;
                        }
                        if (flowInput is not null)
                        {
                            _flowMakerEditViewModel.InsertConverterInput(input, flowInput);
                        }
                        else
                        {
                            if (input.HasOption)
                            {
                                input.Mode = InputMode.Option;
                            }
                        }
                        Inputs.Add(input);
                    }
                    if (item.IsOutput)
                    {
                        var outputInfo = flowStep?.Outputs.FirstOrDefault(c => c.Name == item.Name);
                        var output = new FlowStepOutputViewModel(item.Name, item.DisplayName, item.Type);
                        if (outputInfo is not null)
                        {
                            output.Mode = outputInfo.Mode;

                            output.GlobalDataName = outputInfo.GlobalDataName;
                        }
                        Outputs.Add(output);
                    }
                }
            }
        }


        ShowInput = Inputs.Count > 0;
        ShowOutput = Outputs.Count > 0;
    }

    [Reactive]
    public ObservableCollection<FlowStepOutputViewModel> Outputs { get; set; } = [];
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = [];

    [Reactive]
    public bool ShowInput { get; set; }
    [Reactive]
    public bool ShowOutput { get; set; }
    [Reactive]
    public bool ShowSubSteps { get; set; }

    /// <summary>
    /// 步骤唯一Id
    /// </summary>
    [Reactive]
    public Guid Id { get; set; }
    [Reactive]
    public string? DisplayName { get; set; }
    [Reactive]
    public bool Parallel { get; set; }
    [Reactive]
    public string? Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    [Reactive]
    public string? Name { get; set; }


    /// <summary>
    /// 超时,秒
    /// </summary>
    [Reactive]
    public FlowStepInputViewModel TimeOut { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    [Reactive]
    public FlowStepInputViewModel Retry { get; set; }
    /// <summary>
    /// 重复
    /// </summary>
    [Reactive]
    public FlowStepInputViewModel Repeat { get; set; }

    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    [Reactive]
    public FlowStepInputViewModel ErrorHandling { get; set; }

    /// <summary>
    /// 是否为错误后执行的步骤
    /// </summary>
    [Reactive]
    public FlowStepInputViewModel Finally { get; set; }
    /// <summary>
    /// 前置任务
    /// </summary>
    public List<Guid> PreSteps { get; set; } = [];



    [Reactive]
    public ObservableCollection<EventViewModel> WaitEvents { get; set; } = [];

    [Reactive]
    public ObservableCollection<FlowIfViewModel> Conditions { get; set; } = [];
    /// <summary>
    /// 步骤状态
    /// </summary>
    [Reactive]
    public StepStatus Status { get; set; }

    /// <summary>
    /// 持续时间
    /// </summary>
    [Reactive]
    public TimeSpan Time { get; set; } = TimeSpan.FromSeconds(1);
    [Reactive]
    public TimeSpan PreTime { get; set; }

}

public class EventViewModel : ReactiveObject
{
    public string? Name { get; set; }
}

public enum StepStatus
{
    /// <summary>
    /// 默认
    /// </summary>
    Normal,
    /// <summary>
    /// 选中
    /// </summary>
    Selected,
    /// <summary>
    /// 依赖错误
    /// </summary>
    DependencyError,
    /// <summary>
    /// 前置任务
    /// </summary>
    PreStep,
    /// <summary>
    /// 间接前置任务
    /// </summary>
    IndirectPreStep
}
