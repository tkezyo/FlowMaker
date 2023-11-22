using FlowMaker;
using FlowMaker.Models;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI.Notifications;

namespace Test1.ViewModels;

public class FlowMakerConfigEditViewModel : ViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    private readonly FlowManager _flowManager;

    public FlowMakerConfigEditViewModel(IOptions<FlowMakerOption> options, FlowManager flowManager)
    {
        SaveCommand = ReactiveCommand.CreateFromTask(Save);
        _flowMakerOption = options.Value;
        this._flowManager = flowManager;
        foreach (var item in Enum.GetValues<ErrorHandling>())
        {
            ErrorHandlings.Add(item);
        }
    }
    private IStepDefinition? _stepDefinition;

    public void SetStepDefinition(IStepDefinition stepDefinition)
    {
        _stepDefinition = stepDefinition;
        Model.FlowCategory = stepDefinition.Category;
        Model.FlowName = stepDefinition.Name;
        foreach (var item in stepDefinition.Data)
        {
            if (item.IsInput)
            {
                Model.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
            }
        }
    }

    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];
    public async Task LoadConfig(string category, string name, string flowCategory, string flowName)
    {
        var cd = await _flowManager.LoadConfigDefinitionAsync(category, name, flowCategory, flowName);
        if (cd is null)
        {
            throw new Exception();
        }
        await Load(flowCategory, flowName);
        Model.Name = cd.Name;
        Model.Category = cd.Category;
        foreach (var item in cd.Data)
        {
            var data = Model.Data.FirstOrDefault(c => c.Name == item.Name);
            if (data is not null)
            {
                data.Value = item.Value;
            }
        }
    }
    public async Task Load(string category, string name)
    {
        var step = _flowMakerOption.GetStep(category, name);
        if (step is not null)
        {
            SetStepDefinition(step);
        }
        else
        {
            var flow = await _flowManager.LoadFlowDefinitionAsync(category, name);
            if (flow is not null)
            {
                SetStepDefinition(flow);
            }
            else
            {
                throw new System.Exception("未找到步骤");
            }
        }
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; set; }
    public async Task Save()
    {
        if (string.IsNullOrEmpty(Model.Category) || string.IsNullOrEmpty(Model.Name) || string.IsNullOrEmpty(Model.FlowCategory) || string.IsNullOrEmpty(Model.FlowName))
        {
            return;
        }
        ConfigDefinition configDefinition = new ConfigDefinition()
        {
            Category = Model.Category,
            FlowCategory = Model.FlowCategory,
            FlowName = Model.FlowName,
            Name = Model.Name,
            ErrorHandling = Model.ErrorHandling,
            Repeat = Model.Repeat,
            Retry = Model.Retry,
        };
        foreach (var item in Model.Data)
        {
            if (string.IsNullOrEmpty(item.Value))
            {
                return;
            }
            configDefinition.Data.Add(new NameValue(item.Name, item.Value));
        }
        await _flowManager.SaveConfig(configDefinition);

        CloseModal(true);
    }
    [Reactive]
    public ConfigDefinitionViewModel Model { get; set; } = new();
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