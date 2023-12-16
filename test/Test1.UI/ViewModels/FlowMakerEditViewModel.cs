using DynamicData;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.Services;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Test1.ViewModels;

public class FlowMakerEditViewModel : ViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    private readonly FlowManager _flowManager;
    private readonly IMessageBoxManager _messageBoxManager;

    public FlowMakerEditViewModel(IOptions<FlowMakerOption> options, FlowManager flowManager, IMessageBoxManager messageBoxManager)
    {
        _flowMakerOption = options.Value;
        CreateCommand = ReactiveCommand.CreateFromTask(Create);
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
        RunCommand = ReactiveCommand.CreateFromTask<FlowStepViewModel>(Run);

        AddCheckerCommand = ReactiveCommand.Create(AddChecker);
        RemoveCheckerCommand = ReactiveCommand.Create<FlowStepInputViewModel>(RemoveChecker);
        LoadIfCommand = ReactiveCommand.Create<FlowStepViewModel>(LoadIf);
        AddWaitEventCommand = ReactiveCommand.Create(AddWaitEvent);
        RemoveWaitEventCommand = ReactiveCommand.Create<string>(RemoveWaitEvent);

        ShowSubFlowCommand = ReactiveCommand.CreateFromTask<FlowStepViewModel>(ShowSubFlowAsync);

        foreach (var item in _flowMakerOption.Group)
        {
            StepGroups.Add(item.Key);
        }
        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }

        this._flowManager = flowManager;
        this._messageBoxManager = messageBoxManager;
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
            };
            foreach (var subInput in flowStepInputViewModel.SubInputs)
            {
                flowInput.Inputs.Add(CreateInput(subInput));
            }
            return flowInput;
        }

        FlowDefinition flowDefinition = new() { Category = Category, Name = Name };
        foreach (var item in Steps)
        {
            var f = new FlowStep()
            {
                Category = item.Category,
                DisplayName = item.DisplayName,
                Name = item.Name,
                Compensate = item.Compensate,
                ErrorHandling = CreateInput(item.ErrorHandling),
                Id = item.Id,
                Repeat = CreateInput(item.Repeat),
                Retry = CreateInput(item.Retry),
                TimeOut = CreateInput(item.TimeOut),
                IsSubFlow = item.IsSubFlow,
            };
            foreach (var ifItem in item.Ifs)
            {
                if (ifItem.Enable)
                {
                    f.Ifs.Add(ifItem.Id, ifItem.IsTrue);
                }
            }
            foreach (var wait in item.WaitEvents)
            {
                f.WaitEvents.Add(new FlowWait
                {
                    Type = EventType.Event,
                    EventName = wait
                });
            }
            foreach (var preStep in item.PreSteps)
            {
                f.WaitEvents.Add(new FlowWait
                {
                    Type = EventType.Step,
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

            flowDefinition.Steps.Add(f);
        }
        foreach (var item in Checkers)
        {
            flowDefinition.Checkers.Add(CreateInput(item));
        }
        foreach (var item in GlobeDatas)
        {
            var data = new StepDataDefinition(item.Name, item.DisplayName, item.Type, item.DefaultValue)
            {
                IsInput = item.IsInput,
                IsOutput = item.IsOutput,
                FromStepId = item.FromStepId,
                FromStepPropName = item.FromStepPropName,
            };
            foreach (var option in item.Options)
            {
                data.Options.Add(new OptionDefinition(option.DisplayName, option.Name));
            }
            flowDefinition.Data.Add(data);
        }
        await _flowManager.SaveFlow(flowDefinition);
        CloseModal(true);
    }
    public async Task Load(string? category = null, string? name = null)
    {
        foreach (var item in _flowManager.LoadFlowCategories())
        {
            StepGroups.Add(item);
        }
        FlowDefinition flowDefinition;
        try
        {
            flowDefinition = await _flowManager.LoadFlowDefinitionAsync(category, name);
        }
        catch
        {
            return;
        }

        Category = flowDefinition.Category;
        Name = flowDefinition.Name;
        Steps.Clear();
        Checkers.Clear();
        GlobeDatas.Clear();

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
                Type = item.Type,
                IsStepOutput = item.FromStepId.HasValue,
            };
            foreach (var option in item.Options)
            {
                data.Options.Add(new FlowStepOptionViewModel(option.Name, option.DisplayName));
            }
            GlobeDatas.Add(data);
        }

        foreach (var item in flowDefinition.Checkers)
        {
            Checkers.Add(new FlowStepInputViewModel("", item.Name, "bool", this)
            {
                Value = item.Value,
                Mode = item.Mode,
                Id = item.Id,
                ConverterCategory = item.ConverterCategory,
                ConverterName = item.ConverterName,
            });
        }

        foreach (var item in flowDefinition.Steps)
        {
            FlowStepViewModel flowStepViewModel = new(this)
            {
                DisplayName = item.DisplayName,
                Compensate = item.Compensate,
                Id = item.Id,
                Status = StepStatus.Normal,
                Time = TimeSpan.FromSeconds(1),
            };
            flowStepViewModel.Category = item.Category;
            flowStepViewModel.Name = item.Name;
            await flowStepViewModel.SetInputOutputs(item);
            flowStepViewModel.Init();

            flowStepViewModel.ErrorHandling = new FlowStepInputViewModel("ErrorHandling", "ErrorHandling", "ErrorHandling", this)
            {
                Value = item.ErrorHandling.Value,
                Mode = item.ErrorHandling.Mode,
                Id = item.ErrorHandling.Id,
                ConverterCategory = item.ErrorHandling.ConverterCategory,
                ConverterName = item.ErrorHandling.ConverterName,
            };
            flowStepViewModel.Repeat = new FlowStepInputViewModel("Repeat", "Repeat", "int", this)
            {
                Value = item.Repeat.Value,
                Mode = item.Repeat.Mode,
                Id = item.Repeat.Id,
                ConverterCategory = item.Repeat.ConverterCategory,
                ConverterName = item.Repeat.ConverterName,
            };
            flowStepViewModel.Retry = new FlowStepInputViewModel("Retry", "Retry", "int", this)
            {
                Value = item.Retry.Value,
                Mode = item.Retry.Mode,
                Id = item.Retry.Id,
                ConverterCategory = item.Retry.ConverterCategory,
                ConverterName = item.Retry.ConverterName,
            };
            flowStepViewModel.TimeOut = new FlowStepInputViewModel("TimeOut", "TimeOut", "double", this)
            {
                Value = item.TimeOut.Value,
                Mode = item.TimeOut.Mode,
                Id = item.TimeOut.Id,
                ConverterCategory = item.TimeOut.ConverterCategory,
                ConverterName = item.TimeOut.ConverterName,
            };
            foreach (var wait in item.WaitEvents)
            {
                switch (wait.Type)
                {
                    case EventType.Step when wait.StepId.HasValue:
                        flowStepViewModel.PreSteps.Add(wait.StepId.Value);
                        break;
                    case EventType.Event when !string.IsNullOrEmpty(wait.EventName):
                        flowStepViewModel.WaitEvents.Add(wait.EventName);
                        break;
                    case EventType.EventData:
                        break;
                    case EventType.StartFlow:
                        break;
                    default:
                        break;
                }
            }
            //checker
            foreach (var checker in item.Checkers)
            {
                flowStepViewModel.Checkers.Add(new FlowStepInputViewModel("", checker.Name, "bool", this)
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
            foreach (var ifItem in item.Ifs)
            {
                var entity = flowStepViewModel.Ifs.FirstOrDefault(c => c.Id == ifItem.Key);
                if (entity is not null)
                {
                    entity.Enable = true;
                    entity.IsTrue = ifItem.Value;
                }
            }


            Steps.Add(flowStepViewModel);

        }


        Render();
    }

    public ReactiveCommand<FlowStepViewModel, Unit> RunCommand { get; }
    public async Task Run(FlowStepViewModel flowStepViewModel)
    {
        if (string.IsNullOrEmpty(flowStepViewModel.Category) || string.IsNullOrEmpty(flowStepViewModel.Name))
        {
            return;
        }
        var vm = Navigate<FlowMakerConfigEditViewModel>(HostScreen);
        await vm.Load(flowStepViewModel.Category, flowStepViewModel.Name);

        await _messageBoxManager.Modals.Handle(new ModalInfo("配置", vm));
    }
    #region Steps

    #region Edit

    [Reactive]
    public ObservableCollection<string> StepGroups { get; set; } = [];

    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];
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
        FlowStep.Checkers.Add(new FlowStepInputViewModel("", "", "bool", this));
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
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = [];
    public async Task Create()
    {
        var model = new FlowStepViewModel(this);
        model.Init();
        if (FlowStep is not null)
        {
            model.PreSteps.Add(FlowStep.Id);
            Steps.Insert(Steps.IndexOf(FlowStep) + 1, model);
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

        Steps.Remove(temp);
        Render();
    }
    [Reactive]
    public FlowStepViewModel? FlowStep { get; set; }
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
            }
            else
            {
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
        var oldIndex = Steps.IndexOf(action);
        if (oldIndex >= 0)
        {
            if (up && oldIndex != 0 || !up && oldIndex != Steps.Count - 1)
            {
                Steps.Move(oldIndex, up ? --oldIndex : ++oldIndex);
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
        Checkers.Add(new FlowStepInputViewModel("", "", "bool", this));
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
        _messageBoxManager.Modals.Handle(new FlowMaker.Services.ModalInfo("牛马编辑器:" + flowStepViewModel.Name, vm) { OwnerTitle = WindowTitle }).Subscribe();
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
            foreach (var item in _flowManager.LoadFlows(flowStepViewModel.Category))
            {
                flowStepViewModel.StepDefinitions.Add(item.Name);
            }
        }
    }
    public async Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name)
    {
        return await _flowManager.GetStepDefinitionAsync(category, name);
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
        flowStepInputViewModel.SubInputs.Clear();
        if (flowStepInputViewModel.Mode == InputMode.Converter && !string.IsNullOrWhiteSpace(flowStepInputViewModel.ConverterCategory) && !string.IsNullOrWhiteSpace(flowStepInputViewModel.ConverterName))
        {
            var converter = _flowMakerOption.GetConverter(flowStepInputViewModel.ConverterCategory, flowStepInputViewModel.ConverterName);
            if (converter is null)
            {
                return;
            }
            for (int i = 0; i < converter.Inputs.Count; i++)
            {
                var item = converter.Inputs[i];
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, this);
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
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, this);
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
    public FlowStepInputViewModel(string name, string displayName, string type, FlowMakerEditViewModel flowStepEditViewModel)
    {
        Id = Guid.NewGuid();
        Name = name;
        DisplayName = displayName;
        Type = type;
        this._flowStepEditViewModel = flowStepEditViewModel;


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
            _flowStepEditViewModel.InsertConverterInput(this);
            _flowStepEditViewModel.InitGlobe(this);
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
    public bool HasConverter { get; set; }
    [Reactive]
    public bool HasGlobe { get; set; }
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
        this._flowStepViewModel = flowStepViewModel;


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


    public FlowStepViewModel(FlowMakerEditViewModel flowMakerEditViewModel)
    {
        Id = Guid.NewGuid();
        this._flowMakerEditViewModel = flowMakerEditViewModel;
        ErrorHandling = new FlowStepInputViewModel("ErrorHandling", "错误处理", "ErrorHandling", _flowMakerEditViewModel)
        {
            Value = "",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Repeat = new FlowStepInputViewModel("Repeat", "重复", "int", _flowMakerEditViewModel)
        {
            Value = "",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        Retry = new FlowStepInputViewModel("Retry", "重试", "int", _flowMakerEditViewModel)
        {
            Value = "",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
        TimeOut = new FlowStepInputViewModel("TimeOut", "超时", "double", _flowMakerEditViewModel)
        {
            Value = "",
            Mode = InputMode.Normal,
            Id = Guid.NewGuid(),
        };
    }

    public void Init()
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
    /// <summary>
    /// 子任务
    /// </summary>
    [Reactive]
    public bool IsSubFlow { get; set; }

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
        IsSubFlow = stepDef is FlowDefinition;

        Outputs.Clear();
        Inputs.Clear();

        foreach (var item in stepDef.Data)
        {
            if (item.IsInput)
            {
                var flowInput = flowStep?.Inputs.FirstOrDefault(c => c.Name == item.Name);

                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerEditViewModel);
                if (item.Options.Count != 0)
                {
                    input.HasOption = true;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }
                if (flowInput is not null)
                {
                    input.Mode = flowInput.Mode;
                    input.ConverterCategory = flowInput.ConverterCategory;
                    input.ConverterName = flowInput.ConverterName;
                    input.Id = flowInput.Id;
                    input.Value = flowInput.Value;
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
                var outputinfo = flowStep?.Outputs.FirstOrDefault(c => c.Name == item.Name);
                var output = new FlowStepOutputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerEditViewModel);
                if (outputinfo is not null)
                {
                    output.Mode = outputinfo.Mode;
                    output.ConverterCategory = outputinfo.ConverterCategory;
                    output.ConverterName = outputinfo.ConverterName;
                    output.GlobeDataName = outputinfo.GlobeDataName;
                    _flowMakerEditViewModel.InsertConverterInput(output, outputinfo);
                    output.InputKey = outputinfo.InputKey;
                }
                Outputs.Add(output);
            }
        }
    }
    [Reactive]
    public ObservableCollection<string> StepDefinitions { get; set; } = [];
    [Reactive]
    public ObservableCollection<FlowStepOutputViewModel> Outputs { get; set; } = [];
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = [];

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
    /// 前置任务
    /// </summary>
    public List<Guid> PreSteps { get; set; } = [];
    /// <summary>
    /// 回退任务
    /// </summary>
    public Guid? Compensate { get; set; }


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
