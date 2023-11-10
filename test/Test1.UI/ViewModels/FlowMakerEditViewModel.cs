using DynamicData;
using FlowMaker;
using FlowMaker.Models;
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
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Test1.ViewModels;

[Dependency(ServiceLifetime.Transient)]
public class FlowMakerEditViewModel : RoutableViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    public FlowMakerEditViewModel(IOptions<FlowMakerOption> options)
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



        AddCheckerCommand = ReactiveCommand.Create(AddChecker);
        RemoveCheckerCommand = ReactiveCommand.Create<FlowStepInputViewModel>(RemoveChecker);
        LoadIfCommand = ReactiveCommand.Create(LoadIf);
        AddWaitEventCommand = ReactiveCommand.Create(AddWaitEvent);
        RemoveWaitEventCommand = ReactiveCommand.Create<string>(RemoveWaitEvent);
        this.WhenAnyValue(c => c.FlowStep!.Category).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            if (_flowMakerOption.Group.TryGetValue(c, out var group))
            {
                StepDefinitions.Clear();

                foreach (var item in group.StepDefinitions)
                {
                    StepDefinitions.Add(item);
                }
            }
        });

        this.WhenAnyValue(c => c.FlowStep!.Name).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            var stepDef = StepDefinitions.FirstOrDefault(v => v.Name == c);
            if (stepDef is null)
            {
                return;
            }
            FlowStep?.SetInputOutputs(this, stepDef);
        });
        foreach (var item in _flowMakerOption.Group)
        {
            StepGroups.Add(item.Key);
        }
        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
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
            FlowInput flowInput = new FlowInput()
            {
                Id = flowStepInputViewModel.Id,
                Name = flowStepInputViewModel.Name,
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
                ErrorHandling = item.ErrorHandling,
                Id = item.Id,
                Repeat = item.Repeat,
                Retry = item.Retry,
                TimeOut = item.TimeOut,
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
                foreach (var input in output.AllInputs)
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
            flowDefinition.Datas.Add(data);
        }
        await Task.CompletedTask;
    }
    public async Task Load(string category, string name)
    {
        string json = "";
        var flowDefinition = JsonSerializer.Deserialize<FlowDefinition>(json);
        if (flowDefinition is null)
        {
            return;
        }
        Category = flowDefinition.Category;
        Name = flowDefinition.Name;
        Steps.Clear();
        Checkers.Clear();
        GlobeDatas.Clear();

        foreach (var item in flowDefinition.Datas)
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
            Checkers.Add(new FlowStepInputViewModel(item.Name, item.Name, "bool", this)
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
            FlowStepViewModel flowStepViewModel = new()
            {
                Name = item.Name,
                DisplayName = item.DisplayName,
                Category = item.Category,
                Compensate = item.Compensate,
                ErrorHandling = item.ErrorHandling,
                Id = item.Id,
                Repeat = item.Repeat,
                Retry = item.Retry,
                Status = StepStatus.Normal,
                TimeOut = item.TimeOut,
                Time = TimeSpan.FromSeconds(1),
            };
            foreach (var input in item.Inputs)
            {

            }
            foreach (var output in item.Outputs)
            {

            }
            foreach (var wait in item.WaitEvents)
            {
                switch (wait.Type)
                {
                    case EventType.Step:
                        flowStepViewModel.PreSteps.Add(wait.StepId.Value);
                        break;
                    case EventType.Event:
                        flowStepViewModel.WaitEvents.Add(wait.EventName);
                        break;
                    case EventType.EventData:
                        break;
                    case EventType.Debug:
                        flowStepViewModel.IsDebug = true;
                        break;
                    case EventType.StartFlow:
                        break;
                    default:
                        break;
                }
            }
        }

        await Task.CompletedTask;
    }

    #region Steps

    #region Edit

    [Reactive]
    public ObservableCollection<string> StepGroups { get; set; } = new ObservableCollection<string>();
    [Reactive]
    public ObservableCollection<StepDefinition> StepDefinitions { get; set; } = new ObservableCollection<StepDefinition>();
    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = new();
    public ReactiveCommand<Unit, Unit> LoadIfCommand { get; }
    public void LoadIf()
    {
        if (FlowStep is null)
        {
            return;
        }
        foreach (var item in Checkers)
        {
            var r = FlowStep.Ifs.FirstOrDefault(c => c.Id == item.Id);
            if (r is null)
            {
                FlowStep.Ifs.Add(new FlowIfViewModel
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
        foreach (var item in FlowStep.Checkers)
        {
            var r = FlowStep.Ifs.FirstOrDefault(c => c.Id == item.Id);
            if (r is null)
            {
                FlowStep.Ifs.Add(new FlowIfViewModel
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
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = new();
    public async Task Create()
    {
        if (FlowStep is not null)
        {
            var model = new FlowStepViewModel();
            model.PreSteps.Add(FlowStep.Id);
            Steps.InsertAfter(FlowStep, model);
            ChangePre(FlowStep);
            ChangePre(model);
        }
        else
        {
            var model = new FlowStepViewModel();
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
                var output = GlobeDatas.FirstOrDefault(c => c.FromStepId == FlowStep.Id && c.Name == item.Name);
                if (output is null)
                {
                    GlobeDatas.Add(new StepDataDefinitionViewModel
                    {
                        Name = item.Name,
                        DisplayName = item.DisplayName,
                        FromStepPropName = FlowStep.DisplayName,
                        FromStepId = FlowStep.Id,
                        IsStepOutput = true,
                        Type = item.Type,
                    });
                }
                else
                {
                    output.DisplayName = item.DisplayName;
                    output.FromStepPropName = FlowStep.DisplayName;
                }
            }
            else
            {
                var output = GlobeDatas.FirstOrDefault(c => c.FromStepId == FlowStep.Id && c.Name == item.Name);
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
        var oldIndex = Steps.FindIndex(c => c.Id == action.Id);
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
    public ObservableCollection<TimeSpan> DateAxis { get; set; } = new ObservableCollection<TimeSpan>();

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
            if (item.PreSteps.Any())
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
        List<FlowStepViewModel> result = new();

        List<Guid> total = ganttActions.Select(c => c.Id).ToList();

        List<(Guid, Guid)> temp = new();

        foreach (var action in ganttActions)
        {
            foreach (var item in action.PreSteps)
            {
                temp.Add((action.Id, item));
            }
        }

        while (total.Any())
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
    public ObservableCollection<FlowStepInputViewModel> Checkers { get; set; } = new();
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
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = new();

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
    public void InsertConverterInput(FlowStepInputViewModel flowStepInputViewModel)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
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
                if (item.Options.Any())
                {
                    input.HasOption = true;
                    input.Mode = InputMode.Option;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }


                flowStepInputViewModel.SubInputs.Add(input);
            }
        }
    }
    public void InsertConverterInput(FlowStepOutputViewModel flowStepOutputViewModel)
    {
        if (_flowMakerOption is null)
        {
            return;
        }
        flowStepOutputViewModel.AllInputs.Clear();
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
                if (item.Options.Any())
                {
                    input.HasOption = true;
                    input.Mode = InputMode.Option;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }
                if (flowStepOutputViewModel.Type == item.Type)
                {
                    flowStepOutputViewModel.InputKeys.Add(new NameValue(item.DisplayName, item.Name));
                }
                flowStepOutputViewModel.AllInputs.Add(input);
            }
        }
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
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = new();
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


        this.WhenAnyValue(c => c.Type).WhereNotNull().Subscribe(c =>
        {
            _flowStepEditViewModel.InitType(this);
        });
        this.WhenAnyValue(c => c.ConverterCategory).WhereNotNull().Subscribe(c =>
        {
            _flowStepEditViewModel.InitConverterDefinitions(this);
        });

        this.WhenAnyValue(c => c.ConverterName).WhereNotNull().Subscribe(c =>
        {
            SubInputs.Clear();

            _flowStepEditViewModel.InsertConverterInput(this);
        });

        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            SubInputs.Clear();
            _flowStepEditViewModel.InsertConverterInput(this);
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
    public ObservableCollection<FlowStepInputViewModel> SubInputs { get; set; } = new();



    [Reactive]
    public ObservableCollection<string> ConverterCategorys { get; set; } = new ObservableCollection<string>();
    [Reactive]
    public ObservableCollection<ConverterDefinition> ConverterDefinitions { get; set; } = new ObservableCollection<ConverterDefinition>();


    [Reactive]
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = new();

    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = new();
}


public class FlowStepOptionViewModel : ReactiveObject
{
    [Reactive]
    public string Name { get; set; }
    [Reactive]
    public string DisplayName { get; set; }

    public FlowStepOptionViewModel(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
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
            foreach (var item in AllInputs)
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

    public ObservableCollection<NameValue> InputKeys { get; set; } = new();


    public string? GlobeDataName { get; set; }

    public ObservableCollection<FlowStepInputViewModel> AllInputs { get; set; } = new ObservableCollection<FlowStepInputViewModel>();
    [Reactive]
    public ObservableCollection<string> ConverterCategorys { get; set; } = new ObservableCollection<string>();
    [Reactive]
    public ObservableCollection<ConverterDefinition> ConverterDefinitions { get; set; } = new ObservableCollection<ConverterDefinition>();

}
public class FlowStepViewModel : ReactiveObject
{
    public FlowStepViewModel()
    {
        Id = Guid.NewGuid();
        ToggleDebugCommand = ReactiveCommand.Create(ToggleDebug);
    }
    [Reactive]
    public ObservableCollection<FlowStepOutputViewModel> Outputs { get; set; } = new();
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = new();
    public void SetInputOutputs(FlowMakerEditViewModel flowMakerEditViewModel, StepDefinition stepDef)
    {
        Outputs.Clear();
        Inputs.Clear();

        foreach (var item in stepDef.Datas)
        {
            if (item.IsInput)
            {
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, flowMakerEditViewModel);
                if (item.Options.Any())
                {
                    input.Mode = InputMode.Option;
                    input.HasOption = true;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }
                Inputs.Add(input);
            }
            if (item.IsOutput)
            {
                Outputs.Add(new FlowStepOutputViewModel(item.Name, item.DisplayName, item.Type, flowMakerEditViewModel));
            }
        }
    }

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
    public double TimeOut { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    [Reactive]
    public int Retry { get; set; } = 0;
    /// <summary>
    /// 重复
    /// </summary>
    [Reactive]
    public int Repeat { get; set; } = 1;
    [Reactive]
    public bool IsDebug { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    [Reactive]
    public ErrorHandling ErrorHandling { get; set; }
    /// <summary>
    /// 前置任务
    /// </summary>
    public List<Guid> PreSteps { get; set; } = new();
    /// <summary>
    /// 回退任务
    /// </summary>
    public Guid? Compensate { get; set; }

    public ReactiveCommand<Unit, Unit> ToggleDebugCommand { get; }
    public void ToggleDebug()
    {
        IsDebug = !IsDebug;
    }

    [Reactive]
    public ObservableCollection<FlowIfViewModel> Ifs { get; set; } = new();

    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Checkers { get; set; } = new();

    [Reactive]
    public ObservableCollection<string> WaitEvents { get; set; } = new();


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
