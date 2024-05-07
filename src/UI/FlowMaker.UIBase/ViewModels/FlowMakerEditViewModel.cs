using DynamicData;
using DynamicData.Binding;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Ty.Services;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public class FlowMakerEditViewModel : ViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    private readonly FlowManager _flowManager;
    private readonly IMessageBoxManager _messageBoxManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFlowProvider _flowProvider;

    public FlowMakerEditViewModel(IOptions<FlowMakerOption> options, FlowManager flowManager, IMessageBoxManager messageBoxManager, IServiceProvider serviceProvider, IFlowProvider flowProvider)
    {
        _flowMakerOption = options.Value;
        CreateCommand = ReactiveCommand.CreateFromTask<bool>(Create);
        CreateEmbeddedSubEmbeddedCommand = ReactiveCommand.Create<FlowStepViewModel>(CreateEmbeddedSubEmbedded);
        CreateEmbeddedSubStepCommand = ReactiveCommand.Create<FlowStepViewModel>(CreateEmbeddedSubStep);
        CreateGlobeDataCommand = ReactiveCommand.Create(CreateGlobeData);
        ChangeScaleCommand = ReactiveCommand.Create<int>(ChangeScale);
        ChangePreCommand = ReactiveCommand.Create<FlowStepViewModel>(ChangePre);
        UpActionCommand = ReactiveCommand.Create(UpAction);
        DownActionCommand = ReactiveCommand.Create(DownAction);
        DeleteActionCommand = ReactiveCommand.Create(DeleteAction);
        AddFlowCheckerCommand = ReactiveCommand.Create(AddFlowChecker);
        RemoveFlowCheckerCommand = ReactiveCommand.Create<FlowStepInputViewModel>(RemoveFlowChecker);
        RemoveGlobeDataCommand = ReactiveCommand.Create<StepDataDefinitionViewModel>(RemoveGlobeData);
        SaveCommand = ReactiveCommand.CreateFromTask(Save);

        AddCheckerCommand = ReactiveCommand.Create(AddChecker);
        RemoveCheckerCommand = ReactiveCommand.Create<FlowStepInputViewModel>(RemoveChecker);
        LoadIfCommand = ReactiveCommand.Create<FlowStepViewModel>(LoadIf);
        AddWaitEventCommand = ReactiveCommand.Create(AddWaitEvent);
        RemoveWaitEventCommand = ReactiveCommand.Create<string>(RemoveWaitEvent);

        ShowSubFlowCommand = ReactiveCommand.CreateFromTask<FlowStepViewModel>(ShowSubFlowAsync);


        _flowManager = flowManager;
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
        this.WhenAnyValue(c => c.SimpleMode).Skip(1).Subscribe(async c =>
        {
            if (c)
            {
                var result = await _messageBoxManager.Conform.Handle(new ConformInfo("简单模式下，不支持并行执行，是否继续？") { OwnerTitle = WindowTitle });
                if (result)
                {
                    Render();
                }
                else
                {
                    Render();
                    SimpleMode = false;
                }
            }
        });
        this.WhenAnyValue(c => c.Scale).Subscribe(c => Render());
        this.WhenAnyValue(c => c.ShowEdit).Where(c => c).Subscribe(c =>
        {
            var keys = _flowMakerOption.Group.Keys.Union(_flowProvider.LoadCategories());
            foreach (var item in keys)
            {
                if (!StepGroups.Contains(item))
                {
                    StepGroups.Add(item);
                }
            }

            //删除多余的
            var deletes = StepGroups.Where(c => !keys.Contains(c)).ToList();

            foreach (var item in deletes)
            {
                StepGroups.Remove(item);
            }
        });

        this.WhenAnyValue(c => c.FlowStep).WhereNotNull().Subscribe(c =>
        {
            ShowEdit = c is not null;
        });
    }
    [Reactive]
    public string? Category { get; set; }
    [Reactive]
    public string? Name { get; set; }


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
                ConverterCategory = flowStepInputViewModel.ConverterCategory,
                ConverterName = flowStepInputViewModel.ConverterName,
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

        FlowDefinition flowDefinition = new() { Category = Category, Name = Name };

        flowDefinition.SimpleMode = SimpleMode;

        FlowStep CreateFlowStep(FlowStepViewModel item)
        {
            var f = new FlowStep()
            {
                Category = item.Category ?? string.Empty,
                DisplayName = item.DisplayName ?? string.Empty,
                Name = item.Name ?? string.Empty,
                Id = item.Id,
                Type = item.Type,
                Parallel = item.Parallel,
                Time = item.Time,
            };
            if (item.Type == StepType.Embedded)
            {
                f.Category = Category;
                f.Name = Name;

                var embeddedFlow = new EmbeddedFlowDefinition
                {
                    Category = Category,
                    Name = Name,
                    StepId = item.Id,
                };

                foreach (var step in item.Steps)
                {
                    embeddedFlow.Steps.Add(CreateFlowStep(step));
                }

                flowDefinition.EmbeddedFlows.Add(embeddedFlow);
            }

            f.ErrorHandling = CreateInput(item.ErrorHandling);
            f.Repeat = CreateInput(item.Repeat);
            f.Retry = CreateInput(item.Retry);
            f.TimeOut = CreateInput(item.TimeOut);
            f.Finally = CreateInput(item.Finally);
            foreach (var ifItem in item.Ifs)
            {
                if (ifItem.Enable)
                {
                    f.Ifs.Add(ifItem.Id, ifItem.IsTrue);
                }
            }
            foreach (var wait in item.WaitEvents)
            {
                f.WaitEvents.Add(new FlowEvent
                {
                    Type = EventType.Event,
                    EventName = wait
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
            foreach (var checker in item.Checkers)
            {
                f.Checkers.Add(CreateInput(checker));
            }
            foreach (var input in item.Inputs)
            {
                f.Inputs.Add(CreateInput(input));
            }
            foreach (var output in item.Outputs)
            {
                var nOutput = new FlowOutput
                {
                    GlobeDataName = output.GlobeDataName,
                    Name = output.Name,
                    Type = output.Type,
                    Mode = output.Mode,
                    ConverterCategory = output.ConverterCategory,
                    ConverterName = output.ConverterName,
                    InputKey = output.InputKey,
                };
                foreach (var input in output.Inputs)
                {
                    nOutput.Inputs.Add(CreateInput(input));
                }
                if (output.Mode == OutputMode.GlobeWithConverter && !string.IsNullOrEmpty(output.ConverterCategory) && !string.IsNullOrEmpty(output.ConverterName))
                {
                    var converter = _flowMakerOption.GetConverter(output.ConverterCategory, output.ConverterName);
                    if (converter is not null)
                    {
                        nOutput.ConvertToType = converter.Output;
                    }
                }
                f.Outputs.Add(nOutput);
            }
            return f;
        }

        foreach (var item in Steps)
        {
            var f = CreateFlowStep(item);

            flowDefinition.Steps.Add(f);
        }

        foreach (var item in GlobeDatas)
        {
            var data = new DataDefinition(item.Name ?? "", item.DisplayName ?? "", item.Type ?? "", item.DefaultValue)
            {
                IsInput = item.IsInput,
                IsOutput = item.IsOutput,
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

        foreach (var item in Checkers)
        {
            flowDefinition.Checkers.Add(CreateInput(item));
        }
        await _flowProvider.SaveFlow(flowDefinition);
        CloseModal(true);
    }
    public async Task Load(string? category = null, string? name = null)
    {
        Steps.Clear();
        Checkers.Clear();
        GlobeDatas.Clear();

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
        {
            return;
        }
        var flowDefinition = await _flowProvider.LoadFlowDefinitionAsync(category, name);

        if (flowDefinition is null)
        {
            return;
        }

        SimpleMode = flowDefinition.SimpleMode;

        Category = flowDefinition.Category;
        Name = flowDefinition.Name;

        foreach (var item in flowDefinition.Data)
        {
            var data = new StepDataDefinitionViewModel
            {
                IsInput = item.IsInput,
                IsOutput = item.IsOutput,
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
                data.Options.Add(new FlowStepOptionViewModel(option.Name, option.DisplayName));
            }
            GlobeDatas.Add(data);
            data.Type = item.Type;//data添加到GlobeDatas后,再赋值Type可以初始化选项集
        }

        foreach (var item in flowDefinition.Checkers)
        {
            Checkers.Add(new FlowStepInputViewModel("", item.Name, "bool", 0, this)
            {
                Value = item.Value,
                Mode = item.Mode,
                Id = item.Id,
                ConverterCategory = item.ConverterCategory,
                ConverterName = item.ConverterName,
            });
        }

        async Task<FlowStepViewModel> CreateStepViewModelAsync(FlowStep flowStepItem)
        {
            FlowStepViewModel flowStepViewModel = new(this, flowStepItem.Type)
            {
                DisplayName = flowStepItem.DisplayName,
                Id = flowStepItem.Id,
                Status = StepStatus.Normal,
                Time = flowStepItem.Time,
                Parallel = flowStepItem.Parallel,
            };

            flowStepViewModel.Category = flowStepItem.Category;
            flowStepViewModel.Name = flowStepItem.Name;

            if (flowStepItem.Type == StepType.Embedded)
            {
                var embeddedFlow = flowDefinition.EmbeddedFlows.First(c => c.StepId == flowStepItem.Id);
                foreach (var item2 in embeddedFlow.Steps)
                {
                    flowStepViewModel.Steps.Add(await CreateStepViewModelAsync(item2));
                }
            }
            else
            {
                await flowStepViewModel.SetInputOutputs(flowStepItem);
            }

            flowStepViewModel.Init();


            foreach (var wait in flowStepItem.WaitEvents)
            {
                switch (wait.Type)
                {
                    case EventType.PreStep when wait.StepId.HasValue:
                        flowStepViewModel.PreSteps.Add(wait.StepId.Value);
                        break;
                    case EventType.Event when !string.IsNullOrEmpty(wait.EventName):
                        flowStepViewModel.WaitEvents.Add(wait.EventName);
                        break;
                    case EventType.StartFlow:
                        break;
                    default:
                        break;
                }
            }
            //checker
            foreach (var checker in flowStepItem.Checkers)
            {
                flowStepViewModel.Checkers.Add(new FlowStepInputViewModel("", checker.Name, "bool", 0, this)
                {
                    Value = checker.Value,
                    Mode = checker.Mode,
                    Id = checker.Id,
                    ConverterCategory = checker.ConverterCategory,
                    ConverterName = checker.ConverterName,
                });
            }
            LoadIf(flowStepViewModel);

            //if
            foreach (var ifItem in flowStepItem.Ifs)
            {
                var entity = flowStepViewModel.Ifs.FirstOrDefault(c => c.Id == ifItem.Key);
                if (entity is not null)
                {
                    entity.Enable = true;
                    entity.IsTrue = ifItem.Value;
                }
            }


            return flowStepViewModel;
        }

        foreach (var item in flowDefinition.Steps)
        {
            var flowStepViewModel = await CreateStepViewModelAsync(item);

            Steps.Add(flowStepViewModel);
        }


        Render();
    }


    #region Steps

    #region Edit

    [Reactive]
    public ObservableCollection<string> StepGroups { get; set; } = [];


    public ReactiveCommand<FlowStepViewModel, Unit> LoadIfCommand { get; }
    public void LoadIf(FlowStepViewModel flowStepViewModel)
    {
        foreach (var item in Checkers)
        {
            var r = flowStepViewModel.Ifs.FirstOrDefault(c => c.Id == item.Id);
            if (r is null)
            {
                flowStepViewModel.Ifs.Add(new FlowIfViewModel
                {
                    Id = item.Id,
                    IsTrue = true,
                    DisplayName = item.DisplayName,
                });
            }
            else
            {
                r.DisplayName = item.DisplayName;
            }
        }
        foreach (var item in flowStepViewModel.Checkers)
        {
            var r = flowStepViewModel.Ifs.FirstOrDefault(c => c.Id == item.Id);
            if (r is null)
            {
                flowStepViewModel.Ifs.Add(new FlowIfViewModel
                {
                    Id = item.Id,
                    IsTrue = true,
                    DisplayName = item.DisplayName,
                });
            }
            else
            {
                r.DisplayName = item.DisplayName;
            }
        }
    }

    public ReactiveCommand<Unit, Unit> AddWaitEventCommand { get; }
    public void AddWaitEvent()
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.WaitEvents.Add("");
    }
    public ReactiveCommand<string, Unit> RemoveWaitEventCommand { get; }
    public void RemoveWaitEvent(string eventName)
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.WaitEvents.Remove(eventName);
    }

    public ReactiveCommand<Unit, Unit> AddCheckerCommand { get; }
    public void AddChecker()
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.Checkers.Add(new FlowStepInputViewModel("", "", "bool", 0, this));
    }
    public ReactiveCommand<FlowStepInputViewModel, Unit> RemoveCheckerCommand { get; }
    public void RemoveChecker(FlowStepInputViewModel input)
    {
        if (FlowStep is null)
        {
            return;
        }
        FlowStep.Checkers.Remove(input);
    }
    #endregion

    [Reactive]
    public bool ShowEdit { get; set; }
    public ReactiveCommand<bool, Unit> CreateCommand { get; }
    public ReactiveCommand<FlowStepViewModel, Unit> CreateEmbeddedSubEmbeddedCommand { get; }
    public ReactiveCommand<FlowStepViewModel, Unit> CreateEmbeddedSubStepCommand { get; }
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = [];
    public async Task Create(bool embedded)
    {
        var model = new FlowStepViewModel(this, embedded ? StepType.Embedded : StepType.Normal);

        model.Init();
        if (FlowStep is not null)
        {
            var steps = GetParent(FlowStep, Steps);
            if (steps is null)
            {
                return;
            }
            if (!SimpleMode)
            {
                model.PreSteps.Add(FlowStep.Id);
            }
            steps.Insert(steps.IndexOf(FlowStep) + 1, model);
            ChangePre(FlowStep);
            ChangePre(model);
        }
        else
        {
            Steps.Add(model);
            ChangePre(model);
        }
        Render();

        await Task.CompletedTask;
    }

    public void CreateEmbeddedSubEmbedded(FlowStepViewModel flowStepViewModel)
    {
        if (flowStepViewModel is not null && flowStepViewModel.Type == StepType.Embedded)
        {
            var model = new FlowStepViewModel(this, StepType.Embedded);

            model.Init();
            flowStepViewModel.Steps.Add(model);
        }
    }

    public void CreateEmbeddedSubStep(FlowStepViewModel flowStepViewModel)
    {
        if (flowStepViewModel is not null && flowStepViewModel.Type == StepType.Embedded)
        {
            var model = new FlowStepViewModel(this, StepType.Normal);

            model.Init();
            flowStepViewModel.Steps.Add(model);
        }
    }
    private ObservableCollection<FlowStepViewModel>? GetParent(FlowStepViewModel flowStepViewModel, ObservableCollection<FlowStepViewModel> all)
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
        ChangePre(FlowStep);
        var deletes = GlobeDatas.Where(c => c.FromStepId == temp.Id);

        GlobeDatas.RemoveMany(deletes);
        var steps = GetParent(FlowStep, Steps);
        if (steps is not null)
        {
            steps.Remove(temp);
        }
        Render();
    }
    [Reactive]
    public FlowStepViewModel? FlowStep { get; set; }
    /// <summary>
    /// 编辑依赖
    /// </summary>
    [Reactive]
    public bool EditPreStep { get; set; }
    /// <summary>
    /// 修改依赖
    /// </summary>
    public ReactiveCommand<FlowStepViewModel, Unit> ChangePreCommand { get; }
    public void ChangePre(FlowStepViewModel c)
    {
        if (FlowStep is null)//全部未选中
        {
            FlowStep = c;
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
                foreach (var item in Steps)
                {
                    item.Status = StepStatus.Normal;
                }
                ShowEdit = false;
                EditPreStep = false;
            }
            else
            {
                if (SimpleMode || !EditPreStep)
                {
                    foreach (var item in Steps)
                    {
                        item.Status = StepStatus.Normal;
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
        Render();
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
                        Name = item.GlobeDataName,
                        DisplayName = item.GlobeDataName,
                        FromStepPropName = item.Name,
                        FromStepId = FlowStep.Id,
                        IsStepOutput = true,
                        Type = item.Type,
                    });
                }
                else
                {
                    output.Name = item.GlobeDataName;
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
            var action = Steps.FirstOrDefault(v => v.Id == item);
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
                Render();
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

    /// <summary>
    /// 渲染
    /// </summary>
    public void Render()
    {
        RenderSimpleMode();
        var newList = Order(Steps);
        newList.Reverse();
        TimeSpan max = TimeSpan.FromSeconds(0);
        foreach (var item in newList)
        {
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
            if (SimpleMode)
            {
                item.PreTime = TimeSpan.FromSeconds(0);
            }
        }
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
    public List<FlowStepViewModel> Order(IEnumerable<FlowStepViewModel> ganttActions)
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

    #region Checkers

    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Checkers { get; set; } = [];
    public ReactiveCommand<Unit, Unit> AddFlowCheckerCommand { get; }
    public void AddFlowChecker()
    {
        Checkers.Add(new FlowStepInputViewModel("", "", "bool", 0, this));
    }
    public ReactiveCommand<FlowStepInputViewModel, Unit> RemoveFlowCheckerCommand { get; }
    public void RemoveFlowChecker(FlowStepInputViewModel input)
    {
        Checkers.Remove(input);
    }
    #endregion

    #region GlobeDatas
    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = [];

    public ReactiveCommand<Unit, Unit> CreateGlobeDataCommand { get; }
    public void CreateGlobeData()
    {
        GlobeDatas.Add(new StepDataDefinitionViewModel());
    }

    public ReactiveCommand<StepDataDefinitionViewModel, Unit> RemoveGlobeDataCommand { get; }
    public void RemoveGlobeData(StepDataDefinitionViewModel stepDataDefinitionViewModel)
    {
        GlobeDatas.Remove(stepDataDefinitionViewModel);
    }

    #endregion

    #region Input
    public ReactiveCommand<FlowStepViewModel, Unit> ShowSubFlowCommand { get; }
    public async Task ShowSubFlowAsync(FlowStepViewModel flowStepViewModel)
    {
        var vm = Navigate<FlowMakerEditViewModel>(HostScreen);
        await vm.Load(flowStepViewModel.Category, flowStepViewModel.Name);
        _messageBoxManager.Modals.Handle(new Ty.Services.ModalInfo("牛马编辑器:" + flowStepViewModel.Name, vm) { OwnerTitle = WindowTitle }).Subscribe();
    }
    public void SetStepDefinitions(FlowStepViewModel flowStepViewModel)
    {
        flowStepViewModel.StepDefinitions.Clear();

        if (string.IsNullOrEmpty(flowStepViewModel.Category))
        {
            return;
        }
        if (_flowMakerOption.Group.TryGetValue(flowStepViewModel.Category, out var group))
        {
            foreach (var item in group.StepDefinitions)
            {
                flowStepViewModel.StepDefinitions.Add(item.Name);
            }
        }
        else
        {
            foreach (var item in _flowProvider.LoadFlows(flowStepViewModel.Category))
            {
                flowStepViewModel.StepDefinitions.Add(item.Name);
            }
        }
    }
    public async Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name)
    {
        return await _flowProvider.GetStepDefinitionAsync(category, name);
    }



    public void InitType(FlowStepInputViewModel flowStepInputViewModel)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
        flowStepInputViewModel.ConverterCategorys.Clear();
        foreach (var item in _flowMakerOption.Group.Where(v => v.Value.ConverterDefinitions.Any(x => x.Output == flowStepInputViewModel.Type)))
        {
            flowStepInputViewModel.ConverterCategorys.Add(item.Key);
        }
        flowStepInputViewModel.HasConverter = flowStepInputViewModel.ConverterCategorys.Any();
    }
    public void InitGlobe(FlowStepInputViewModel flowStepInputViewModel)
    {
        flowStepInputViewModel.GlobeDatas.Clear();

        foreach (var item in GlobeDatas)
        {
            if (item.Type == flowStepInputViewModel.Type)
            {
                flowStepInputViewModel.GlobeDatas.Add(item);
            }
        }
        flowStepInputViewModel.HasGlobe = flowStepInputViewModel.GlobeDatas.Any();
    }
    public void InitType(FlowStepOutputViewModel flowStepOutputViewModel)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
        flowStepOutputViewModel.ConverterCategorys.Clear();
        foreach (var item in _flowMakerOption.Group.Where(v => v.Value.ConverterDefinitions.Any(x => x.Inputs.Any(b => b.Type == flowStepOutputViewModel.Type))))
        {
            flowStepOutputViewModel.HasConverter = true;
            flowStepOutputViewModel.ConverterCategorys.Add(item.Key);
        }
    }
    public void InitConverterDefinitions(FlowStepInputViewModel flowStepInputViewModel)
    {
        if (_flowMakerOption is null || flowStepInputViewModel.ConverterCategory is null)
        {
            return;
        }
        flowStepInputViewModel.SubInputs.Clear();
        flowStepInputViewModel.ConverterDefinitions.Clear();

        foreach (var item in _flowMakerOption.Group[flowStepInputViewModel.ConverterCategory].ConverterDefinitions.Where(v => v.Output == flowStepInputViewModel.Type))
        {
            flowStepInputViewModel.ConverterDefinitions.Add(item);
        }
    }
    public void InitConverterDefinitions(FlowStepOutputViewModel flowStepOutputViewModel)
    {
        if (_flowMakerOption is null || flowStepOutputViewModel.ConverterCategory is null)
        {
            return;
        }
        flowStepOutputViewModel.ConverterDefinitions.Clear();

        foreach (var item in _flowMakerOption.Group[flowStepOutputViewModel.ConverterCategory].ConverterDefinitions.Where(v => v.Output == flowStepOutputViewModel.Type))
        {
            flowStepOutputViewModel.ConverterDefinitions.Add(item);
        }
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
            flowStepInputViewModel.ConverterCategory = flowInput.ConverterCategory;
            flowStepInputViewModel.ConverterName = flowInput.ConverterName;
            flowStepInputViewModel.Id = flowInput.Id;
            flowStepInputViewModel.Value = flowInput.Value;
        }

        if (flowStepInputViewModel.Mode == InputMode.Converter && !string.IsNullOrWhiteSpace(flowStepInputViewModel.ConverterCategory) && !string.IsNullOrWhiteSpace(flowStepInputViewModel.ConverterName))
        {
            flowStepInputViewModel.SubInputs.Clear();

            var converter = _flowMakerOption.GetConverter(flowStepInputViewModel.ConverterCategory, flowStepInputViewModel.ConverterName);
            if (converter is null)
            {
                return;
            }
            for (int i = 0; i < converter.Inputs.Count; i++)
            {
                var item = converter.Inputs[i];
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, item.Rank, this);
                if (item.Options.Count != 0)
                {
                    input.HasOption = true;
                    foreach (var item2 in item.Options)
                    {
                        input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                    }
                }
                if (flowInput is null)
                {
                    if (item.Options.Count != 0)
                    {
                        input.Mode = InputMode.Option;
                    }
                }
                else
                {
                    var sub = flowInput.Inputs.FirstOrDefault(c => c.Name == item.Name);
                    if (sub is not null)
                    {
                        input.Mode = sub.Mode;
                        input.ConverterCategory = sub.ConverterCategory;
                        input.ConverterName = sub.ConverterName;
                        input.Id = sub.Id;
                        input.Value = sub.Value;

                        InsertConverterInput(input, sub);
                    }
                    else
                    {
                        if (item.Options.Count != 0)
                        {
                            input.Mode = InputMode.Option;
                        }
                    }
                }

                flowStepInputViewModel.SubInputs.Add(input);
            }
        }
        else if (flowStepInputViewModel.Mode == InputMode.Array && !string.IsNullOrEmpty(flowStepInputViewModel.SubType))
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
                    var input = new FlowStepInputViewModel(displayName, displayName, flowStepInputViewModel.SubType, 0, this);

                    var sub = flowInput.Inputs.FirstOrDefault(c => c.Name == displayName);
                    if (sub is not null)
                    {
                        input.Mode = sub.Mode;
                        input.ConverterCategory = sub.ConverterCategory;
                        input.ConverterName = sub.ConverterName;
                        input.Id = sub.Id;
                        input.Value = sub.Value;

                        InsertConverterInput(input, sub);
                    }
                    flowStepInputViewModel.SubInputs.Add(input);
                }
                newDisplayNames.Add(displayName);

                if (!flowStepInputViewModel.SubInputs.Any(s => s.DisplayName == displayName))
                {
                    var input = new FlowStepInputViewModel(displayName, displayName, flowStepInputViewModel.SubType, 0, this);
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
    public void InsertConverterInput(FlowStepOutputViewModel flowStepOutputViewModel, FlowOutput? flowOutput = null)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
        flowStepOutputViewModel.Inputs.Clear();
        flowStepOutputViewModel.InputKeys.Clear();
        if (flowStepOutputViewModel.Mode == OutputMode.GlobeWithConverter && !string.IsNullOrWhiteSpace(flowStepOutputViewModel.ConverterCategory) && !string.IsNullOrWhiteSpace(flowStepOutputViewModel.ConverterName))
        {
            var converter = _flowMakerOption.GetConverter(flowStepOutputViewModel.ConverterCategory, flowStepOutputViewModel.ConverterName);
            if (converter is null)
            {
                return;
            }
            for (int i = 0; i < converter.Inputs.Count; i++)
            {
                var item = converter.Inputs[i];
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, item.Rank, this);
                if (item.Options.Count != 0)
                {
                    input.HasOption = true;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }
                if (flowStepOutputViewModel.Type == item.Type)
                {
                    flowStepOutputViewModel.InputKeys.Add(new NameValue(item.DisplayName, item.Name));
                }

                if (flowOutput is null)
                {
                    if (item.Options.Count != 0)
                    {
                        input.Mode = InputMode.Option;
                    }
                }
                else
                {
                    var sub = flowOutput.Inputs.FirstOrDefault(c => c.Name == item.Name);
                    if (sub is not null)
                    {
                        input.Mode = sub.Mode;
                        input.ConverterCategory = sub.ConverterCategory;
                        input.ConverterName = sub.ConverterName;
                        input.Id = sub.Id;
                        input.Value = sub.Value;

                        InsertConverterInput(input, sub);
                    }
                    else
                    {
                        if (item.Options.Count != 0)
                        {
                            input.Mode = InputMode.Option;
                        }
                    }
                }
                flowStepOutputViewModel.Inputs.Add(input);
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
            var options = await pp.GetOptions();

            flowStepInputViewModel.Options.Clear();
            foreach (var item in options)
            {
                flowStepInputViewModel.Options.Add(new FlowStepOptionViewModel(item.Name, item.Value));
            }


        }
    }
    #endregion


    #region SimpleMode
    [Reactive]
    public bool SimpleMode { get; set; } = true;

    public void RenderSimpleMode()
    {
        if (!SimpleMode)
        {
            return;
        }
        //遍历Steps，第0项没有prestep,其他项的prestep都是自己的前一项
        for (int i = 0; i < Steps.Count; i++)
        {
            var item = Steps[i];
            if (i == 0)
            {
                item.PreSteps.Clear();
            }
            else
            {
                item.PreSteps.Clear();
                item.PreSteps.Add(Steps[i - 1].Id);
            }
        }
    }

    #endregion
}


public enum FlowEditMode
{
    Serial,
    Tree,
    Gantt,
}
public class StepDataDefinitionViewModel : ReactiveObject
{
    public StepDataDefinitionViewModel()
    {
        AddOptionCommand = ReactiveCommand.Create(AddOption);
        RemoveOptionCommand = ReactiveCommand.Create<FlowStepOptionViewModel>(RemoveOption);
    }
    [Reactive]
    public string? Type { get; set; }
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
    public Guid Id { get; set; }
    [Reactive]
    public bool Enable { get; set; }
    [Reactive]
    public bool IsTrue { get; set; }
    [Reactive]
    public string? DisplayName { get; set; }
}
public class FlowStepInputViewModel : ReactiveObject
{
    private readonly FlowMakerEditViewModel _flowStepEditViewModel;

    [Reactive]
    public Guid Id { get; set; }
    public FlowStepInputViewModel(string name, string displayName, string type, int rank, FlowMakerEditViewModel flowStepEditViewModel)
    {
        Id = Guid.NewGuid();
        Name = name;
        DisplayName = displayName;
        Type = type;
        _flowStepEditViewModel = flowStepEditViewModel;
        Rank = rank;
        for (int i = 0; i < rank; i++)
        {
            Dims.Add(new DimViewModel() { Name = (i + 1).ToString() });
        }

        this.WhenAnyValue(c => c.Type).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            _flowStepEditViewModel.InitType(this);
            _flowStepEditViewModel.InitGlobe(this);
        });
        this.WhenAnyValue(c => c.ConverterCategory).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            _flowStepEditViewModel.InitConverterDefinitions(this);
        });

        this.WhenAnyValue(c => c.ConverterName).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            _flowStepEditViewModel.InsertConverterInput(this);
        });

        this.WhenAnyValue(c => c.Mode).DistinctUntilChanged().Subscribe(c =>
        {
            if (c != InputMode.Converter || c == InputMode.Array)
            {
                ConverterCategory = null;
                ConverterName = null;
                SubInputs.Clear();
            }

            _flowStepEditViewModel.InsertConverterInput(this);
            _flowStepEditViewModel.InitGlobe(this);
        });
        Dims.ToObservableChangeSet().SubscribeMany(c =>
        {
            return c.WhenValueChanged(v => v.Count, notifyOnInitialValue: false).WhereNotNull().Throttle(TimeSpan.FromMilliseconds(200)).DistinctUntilChanged().ObserveOn(RxApp.MainThreadScheduler).Subscribe(v =>
            {
                _flowStepEditViewModel.InsertConverterInput(this);
            });
        }).Subscribe();

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
    public bool HasConverter { get; set; }
    [Reactive]
    public bool HasGlobe { get; set; }

    [Reactive]
    public bool IsArray { get; set; }

    [Reactive]
    public string? SubType { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    [Reactive]
    public string DisplayName { get; set; }
    [Reactive]
    public string Type { get; set; }
    [Reactive]
    public string? Value { get; set; }
    [Reactive]
    public int Rank { get; set; }
    [Reactive]
    public ObservableCollection<DimViewModel> Dims { get; set; } = [];

    [Reactive]
    public string? ConverterCategory { get; set; }
    [Reactive]
    public string? ConverterName { get; set; }
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
    public ObservableCollection<string> ConverterCategorys { get; set; } = [];
    [Reactive]
    public ObservableCollection<ConverterDefinition> ConverterDefinitions { get; set; } = [];


    [Reactive]
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];

    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = [];
}
public class DimViewModel : ReactiveObject
{
    [Reactive]
    public required string Name { get; set; }
    [Reactive]
    public int Count { get; set; }
}
public class FlowStepOptionViewModel(string name, string displayName) : ReactiveObject
{
    [Reactive]
    public string Name { get; set; } = name;
    [Reactive]
    public string DisplayName { get; set; } = displayName;
}
public class FlowStepOutputViewModel : ReactiveObject
{
    private readonly FlowMakerEditViewModel _flowStepViewModel;

    public FlowStepOutputViewModel(string name, string displayName, string type, FlowMakerEditViewModel flowStepViewModel)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;
        _flowStepViewModel = flowStepViewModel;


        _flowStepViewModel.InitType(this);

        this.WhenAnyValue(c => c.ConverterCategory).Skip(1).WhereNotNull().Subscribe(c =>
        {
            _flowStepViewModel.InitConverterDefinitions(this);
        });

        this.WhenAnyValue(c => c.ConverterName).Skip(1).WhereNotNull().Subscribe(c =>
        {
            _flowStepViewModel.InsertConverterInput(this);
        });

        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            _flowStepViewModel.InsertConverterInput(this);
        });
        this.WhenAnyValue(c => c.InputKey).WhereNotNull().Subscribe(c =>
        {
            foreach (var item in Inputs)
            {
                if (item.Name == InputKey)
                {
                    item.SetDisable();
                }
                else
                {
                    item.Disable = false;
                }
            }
        });
    }

    [Reactive]
    public string Name { get; set; }
    [Reactive]
    public OutputMode Mode { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    [Reactive]
    public string DisplayName { get; set; }

    [Reactive]
    public string? ConverterCategory { get; set; }
    [Reactive]
    public string? ConverterName { get; set; }
    [Reactive]
    public string? InputKey { get; set; }
    [Reactive]
    public bool HasConverter { get; set; }
    public string Type { get; set; }

    public ObservableCollection<NameValue> InputKeys { get; set; } = [];


    public string? GlobeDataName { get; set; }

    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = [];
    [Reactive]
    public ObservableCollection<string> ConverterCategorys { get; set; } = [];
    [Reactive]
    public ObservableCollection<ConverterDefinition> ConverterDefinitions { get; set; } = [];

}
public class FlowStepViewModel : ReactiveObject
{
    private readonly FlowMakerEditViewModel _flowMakerEditViewModel;

    public FlowMakerEditViewModel MainViewModel => _flowMakerEditViewModel;
    public FlowStepViewModel(FlowMakerEditViewModel flowMakerEditViewModel, StepType stepType)
    {
        Id = Guid.NewGuid();
        Type = stepType;
        IsSubFlow = stepType == StepType.SubFlow;
        IsEmbeddedFlow = stepType == StepType.Embedded;
        _flowMakerEditViewModel = flowMakerEditViewModel;
        ErrorHandling = new FlowStepInputViewModel("ErrorHandling", "错误处理", "FlowMaker.ErrorHandling", 0, _flowMakerEditViewModel)
        {
            Value = "Skip",
            Id = Guid.NewGuid(),
            Mode = InputMode.Option,
            Options = [new FlowStepOptionViewModel("Skip", "跳过"), new FlowStepOptionViewModel("Finally", "停止"), new FlowStepOptionViewModel("Terminate", "立即停止")],
            HasOption = true,
        };
        Repeat = new FlowStepInputViewModel("Repeat", "重复", "int", 0, _flowMakerEditViewModel)
        {
            Value = "1",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Retry = new FlowStepInputViewModel("Retry", "重试", "int", 0, _flowMakerEditViewModel)
        {
            Value = "0",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        TimeOut = new FlowStepInputViewModel("TimeOut", "超时", "double", 0, _flowMakerEditViewModel)
        {
            Value = "0",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Finally = new FlowStepInputViewModel("Finally", "总会执行", "bool", 0, _flowMakerEditViewModel)
        {
            Value = "false",
            Mode = InputMode.Option,
            Id = Guid.NewGuid(),
            Options = [new FlowStepOptionViewModel("true", "是"), new FlowStepOptionViewModel("false", "否")],
            HasOption = true,
        };
    }

    public void Init()
    {
        if (Type != StepType.Embedded)
        {
            this.WhenAnyValue(c => c.Category).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
            {
                _flowMakerEditViewModel.SetStepDefinitions(this);
            });
            this.WhenAnyValue(c => c.Name).Skip(1).WhereNotNull().DistinctUntilChanged().Subscribe(async c =>
            {
                await SetInputOutputs();
            });
        }
        else
        {

        }

    }
    /// <summary>
    /// 任务类型
    /// </summary>
    [Reactive]
    public StepType Type { get; set; }
    [Reactive]
    public bool IsSubFlow { get; set; }
    [Reactive]
    public bool IsEmbeddedFlow { get; set; }
    [Reactive]
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = [];


    public async Task SetInputOutputs(FlowStep? flowStep = null)
    {
        if (string.IsNullOrEmpty(Category) || string.IsNullOrEmpty(Name))
        {
            return;
        }

        var stepDef = await _flowMakerEditViewModel.GetStepDefinitionAsync(Category, Name);
        if (stepDef is null)
        {
            return;
        }

        if (stepDef is IFlowDefinition)
        {
            Type = StepType.SubFlow;
            IsSubFlow = true;
        }


        Outputs.Clear();
        Inputs.Clear();

        if (flowStep is not null)
        {
            _flowMakerEditViewModel.InsertConverterInput(ErrorHandling, flowStep.ErrorHandling);
            _flowMakerEditViewModel.InsertConverterInput(Repeat, flowStep.Repeat);
            _flowMakerEditViewModel.InsertConverterInput(Retry, flowStep.Retry);
            _flowMakerEditViewModel.InsertConverterInput(TimeOut, flowStep.TimeOut);
            _flowMakerEditViewModel.InsertConverterInput(Finally, flowStep.Finally);
        }


        foreach (var item in stepDef.Data)
        {
            if (item.IsInput)
            {
                var flowInput = flowStep?.Inputs.FirstOrDefault(c => c.Name == item.Name);

                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, item.Rank, _flowMakerEditViewModel);
                input.IsArray = item.IsArray;
                input.SubType = item.SubType;

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
                var output = new FlowStepOutputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerEditViewModel);
                if (outputInfo is not null)
                {
                    output.Mode = outputInfo.Mode;
                    output.ConverterCategory = outputInfo.ConverterCategory;
                    output.ConverterName = outputInfo.ConverterName;
                    output.GlobeDataName = outputInfo.GlobeDataName;
                    _flowMakerEditViewModel.InsertConverterInput(output, outputInfo);
                    output.InputKey = outputInfo.InputKey;
                }
                Outputs.Add(output);
            }
        }

        ShowInput = Inputs.Count > 0;
        ShowOutput = Outputs.Count > 0;
    }
    [Reactive]
    public ObservableCollection<string> StepDefinitions { get; set; } = [];
    [Reactive]
    public ObservableCollection<FlowStepOutputViewModel> Outputs { get; set; } = [];
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = [];

    [Reactive]
    public bool ShowInput { get; set; }
    [Reactive]
    public bool ShowOutput { get; set; }

    /// <summary>
    /// 步骤唯一Id
    /// </summary>
    [Reactive]
    public Guid Id { get; set; }
    [Reactive]
    public string? DisplayName { get; set; }
    [Reactive]
    public string? Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    [Reactive]
    public string? Name { get; set; }

    /// <summary>
    /// 并行执行-仅在简单模式下有效
    /// </summary>
    [Reactive]
    public bool Parallel { get; set; }
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
    public ObservableCollection<FlowIfViewModel> Ifs { get; set; } = [];

    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Checkers { get; set; } = [];

    [Reactive]
    public ObservableCollection<string> WaitEvents { get; set; } = [];


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
