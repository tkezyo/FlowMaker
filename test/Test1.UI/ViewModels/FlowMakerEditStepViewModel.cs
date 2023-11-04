using AutoMapper;
using DynamicData;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Volo.Abp;

namespace Test1.ViewModels;

public class FlowMakerEditStepViewModel : RoutableViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    public FlowMakerEditStepViewModel(IOptions<FlowMakerOption> options)
    {
        _flowMakerOption = options.Value;
        AddCheckerCommand = ReactiveCommand.Create(AddChecker);
        RemoveCheckerCommand = ReactiveCommand.Create<FlowStepInputViewModel>(RemoveChecker);
        LoadIfCommand = ReactiveCommand.Create(LoadIf);
        foreach (var item in _flowMakerOption.Group)
        {
            StepGroups.Add(item.Key);
        }
        this.WhenAnyValue(c => c.Model.Category).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
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

        this.WhenAnyValue(c => c.Model.Name).WhereNotNull().DistinctUntilChanged().Subscribe(c =>
        {
            if (_flowDefinition is null)
            {
                throw new Exception();
            }
            var stepDef = StepDefinitions.FirstOrDefault(v => v.Name == c);
            Outputs.Clear();
            Inputs.Clear();
            if (stepDef is not null)
            {
                foreach (var item in stepDef.Outputs)
                {
                    Outputs.Add(new FlowStepOutputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerOption, _flowDefinition));
                }

                foreach (var item in stepDef.Inputs)
                {
                    var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerOption, _flowDefinition);
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
            }
        });

        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
        var dic = new ConcurrentDictionary<string, FlowGlobeData>();
        dic.TryAdd("wer", new FlowGlobeData("wer", "int", "ff"));
        Init(new FlowDefinition
        {
            Category = "",
        });
    }
    private FlowDefinition? _flowDefinition;

    public void Init(FlowDefinition flowDefinition)
    {
        _flowDefinition = flowDefinition;
    }

    [Reactive]
    public ObservableCollection<FlowStepOutputViewModel> Outputs { get; set; } = new();
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Inputs { get; set; } = new();
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> Checkers { get; set; } = new();
    [Reactive]
    public ObservableCollection<FlowIfViewModel> Ifs { get; set; } = new();

    public ReactiveCommand<Unit, Unit> LoadIfCommand { get; }
    public void LoadIf()
    {
        Ifs.Clear();
        foreach (var item in Checkers.Where(c => c.Type == "bool"))
        {
            Ifs.Add(new FlowIfViewModel
            {
                Id = item.Id,
                IsTrue = true,
                DisplayName = item.DisplayName,
            });
        }
    }

    [Reactive]
    public ObservableCollection<string> StepGroups { get; set; } = new ObservableCollection<string>();
    [Reactive]
    public ObservableCollection<StepDefinition> StepDefinitions { get; set; } = new ObservableCollection<StepDefinition>();
    [Reactive]
    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = new();


    public ReactiveCommand<Unit, Unit> AddCheckerCommand { get; }
    public void AddChecker()
    {
        Checkers.Add(new FlowStepInputViewModel("", "", "bool", _flowMakerOption, _flowDefinition!));
    }
    public ReactiveCommand<FlowStepInputViewModel, Unit> RemoveCheckerCommand { get; }
    public void RemoveChecker(FlowStepInputViewModel input)
    {
        Checkers.Remove(input);
    }

    [Reactive]
    public FlowStepViewModel Model { get; set; } = new();
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
    private readonly FlowMakerOption _flowMakerOption;
    private readonly FlowDefinition _flowDefinition;
    [Reactive]
    public Guid Id { get; set; }
    public FlowStepInputViewModel(string name, string displayName, string type, FlowMakerOption flowMakerOption, FlowDefinition flowDefinition)
    {
        Id = Guid.NewGuid();
        Name = name;
        DisplayName = displayName;
        Type = type;
        _flowMakerOption = flowMakerOption;
        this._flowDefinition = flowDefinition;

        this.WhenAnyValue(c => c.Type).WhereNotNull().Subscribe(c =>
        {
            ConverterCategorys.Clear();
            foreach (var item in _flowMakerOption.Group.Where(v => v.Value.ConverterDefinitions.Any(x => x.Output == c)))
            {
                ConverterCategorys.Add(item.Key);
            }
            HasConverter = ConverterCategorys.Any();
            LoadGlobe();
        });
        this.WhenAnyValue(c => c.ConverterCategory).WhereNotNull().Subscribe(c =>
        {
            SubInputs.Clear();
            ConverterDefinitions.Clear();

            foreach (var item in _flowMakerOption.Group[c].ConverterDefinitions.Where(v => v.Output == Type))
            {
                ConverterDefinitions.Add(item);
            }
        });

        this.WhenAnyValue(c => c.ConverterName).WhereNotNull().Subscribe(c =>
        {
            SubInputs.Clear();

            InsertConverterInput();
        });

        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            SubInputs.Clear();
            InsertConverterInput();

        });
    }

    public void LoadGlobe()
    {
        GlobeDatas.Clear();
        foreach (var item in _flowDefinition.Steps.SelectMany(c => c.Outputs))
        {
            var dataType = item.ConvertToType ?? item.Type;

            if (dataType == Type)
            {
                GlobeDatas.Add(item);
            }
        }
        HasGlobe = GlobeDatas.Any();
    }

    public void InsertConverterInput()
    {
        if (Mode == InputMode.Converter && !string.IsNullOrWhiteSpace(ConverterCategory) && !string.IsNullOrWhiteSpace(ConverterName))
        {
            var converter = _flowMakerOption.GetConverter(ConverterCategory, ConverterName);
            if (converter is null)
            {
                return;
            }
            for (int i = 0; i < converter.Inputs.Count; i++)
            {
                var item = converter.Inputs[i];
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerOption, _flowDefinition);
                if (item.Options.Any())
                {
                    input.HasOption = true;
                    input.Mode = InputMode.Option;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }


                SubInputs.Add(input);
            }
        }
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
    public ObservableCollection<FlowOutput> GlobeDatas { get; set; } = new();
}

public enum InputMode
{
    Normal,
    Option,
    Globe,
    Converter,
}
public class FlowStepOptionViewModel : ReactiveObject
{
    [Reactive]
    public string Name { get; set; }
    [Reactive]
    public string DisplayName { get; set; }

    public FlowStepOptionViewModel(string name, string value)
    {
        Name = name;
        DisplayName = value;
    }
}
public enum OutputMode
{
    Drop,
    Globe,
    GlobeWithConverter,
}
public class FlowStepOutputViewModel : ReactiveObject
{
    private readonly FlowMakerOption _flowMakerOption;
    private readonly FlowDefinition _flowDefinition;
    public FlowStepOutputViewModel(string name, string displayName, string type, FlowMakerOption flowMakerOption, FlowDefinition flowDefinition)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;
        _flowMakerOption = flowMakerOption;
        this._flowDefinition = flowDefinition;

        foreach (var item in _flowMakerOption.Group.Where(v => v.Value.ConverterDefinitions.Any(x => x.Inputs.Any(b => b.Type == type))))
        {
            HasConverter = true;
            ConverterCategorys.Add(item.Key);
        }

        this.WhenAnyValue(c => c.ConverterCategory).Skip(1).WhereNotNull().Subscribe(c =>
        {
            ConverterDefinitions.Clear();

            foreach (var item in _flowMakerOption.Group[c].ConverterDefinitions.Where(v => v.Output == type))
            {
                ConverterDefinitions.Add(item);
            }
        });

        this.WhenAnyValue(c => c.ConverterName).Skip(1).WhereNotNull().Subscribe(c =>
        {
            InsertConverterInput();
        });

        this.WhenAnyValue(c => c.Mode).Subscribe(c =>
        {
            InsertConverterInput();
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
    public void InsertConverterInput()
    {
        AllInputs.Clear();
        InputKeys.Clear();
        if (Mode == OutputMode.GlobeWithConverter && !string.IsNullOrWhiteSpace(ConverterCategory) && !string.IsNullOrWhiteSpace(ConverterName))
        {
            var converter = _flowMakerOption.GetConverter(ConverterCategory, ConverterName);
            if (converter is null)
            {
                return;
            }
            for (int i = 0; i < converter.Inputs.Count; i++)
            {
                var item = converter.Inputs[i];
                var input = new FlowStepInputViewModel(item.Name, item.DisplayName, item.Type, _flowMakerOption, _flowDefinition);
                if (item.Options.Any())
                {
                    input.HasOption = true;
                    input.Mode = InputMode.Option;
                }
                foreach (var item2 in item.Options)
                {
                    input.Options.Add(new FlowStepOptionViewModel(item2.DisplayName, item2.Name));
                }
                if (Type == item.Type)
                {
                    InputKeys.Add(new NameValue(item.DisplayName, item.Name));
                }
                AllInputs.Add(input);
            }
        }
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
    /// <summary>
    /// 是否可执行，同时可作为Break的条件
    /// </summary>
    public Dictionary<Guid, bool> If { get; set; } = new();
    public List<FlowInput> Checkers { get; set; } = new();
    /// <summary>
    /// 步骤位置
    /// </summary>
    [Reactive]
    public double Left { get; set; }
    /// <summary>
    /// 步骤位置
    /// </summary>
    [Reactive]
    public double Top { get; set; }
    /// <summary>
    /// 步骤状态
    /// </summary>
    [Reactive]
    public StepStatus Status { get; set; }
 
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
