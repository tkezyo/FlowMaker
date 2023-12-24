//using FlowMaker;
//using FlowMaker.Models;
//using FlowMaker.Persistence;
//using FlowMaker.Services;
//using FlowMaker.ViewModels;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Options;
//using ReactiveUI;
//using ReactiveUI.Fody.Helpers;
//using System;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Reactive;
//using System.Reactive.Disposables;
//using System.Reactive.Linq;
//using System.Threading.Tasks;

//namespace Test1.ViewModels;

//public class FlowMakerConfigEditViewModel : ViewModelBase
//{
//    private readonly FlowMakerOption _flowMakerOption;
//    private readonly FlowManager _flowManager;
//    private readonly IMessageBoxManager _messageBoxManager;
//    private readonly IServiceProvider _serviceProvider;
//    private readonly IFlowProvider _flowProvider;

//    public FlowMakerConfigEditViewModel(IOptions<FlowMakerOption> options, FlowManager flowManager, IMessageBoxManager messageBoxManager, IServiceProvider serviceProvider,IFlowProvider flowProvider)
//    {
//        SaveOrRunCommand = ReactiveCommand.CreateFromTask<bool?>(SaveOrRun);
//        _flowMakerOption = options.Value;
//        this._flowManager = flowManager;
//        this._messageBoxManager = messageBoxManager;
//        this._serviceProvider = serviceProvider;
//        this._flowProvider = flowProvider;
//        foreach (var item in Enum.GetValues<ErrorHandling>())
//        {
//            ErrorHandlings.Add(item);
//        }
//        foreach (var item in _flowMakerOption.Middlewares)
//        {
//            Middlewares.Add(new MiddlewareSelectViewModel(item.Name, item.Value));
//        }

//        this.WhenAnyValue(c => c.Model.FlowCategory).Subscribe(c =>
//        {
//            SetStepDefinitions();
//        });
//        this.WhenAnyValue(c => c.Model.FlowName).Subscribe(async c =>
//        {
//            await SetStepInputAsync();
//        });
//    }


//    private IStepDefinition? _stepDefinition;

//    public void SetStepDefinition(IStepDefinition stepDefinition)
//    {
//        _stepDefinition = stepDefinition;
//        Model.FlowCategory = stepDefinition.Category;
//        Model.FlowName = stepDefinition.Name;
//        Model.Data.Clear();
//        foreach (var item in stepDefinition.Data)
//        {
//            if (item.IsInput)
//            {
//                Model.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
//            }
//        }
//    }

//    public ObservableCollection<MiddlewareSelectViewModel> Middlewares { get; set; } = [];
//    public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];
//    /// <summary>
//    /// 载入配置
//    /// </summary>
//    /// <param name="category"></param>
//    /// <param name="name"></param>
//    /// <param name="flowCategory"></param>
//    /// <param name="flowName"></param>
//    /// <returns></returns>
//    /// <exception cref="Exception"></exception>
//    public async Task LoadConfig(string category, string name, string flowCategory, string flowName)
//    {
//        Type = DefinitionType.Config;
//        var cd = await _flowProvider.LoadConfigDefinitionAsync(category, name, flowCategory, flowName) ?? throw new Exception();

//        await Load(flowCategory, flowName);
//        Model.Name = cd.ConfigName;
//        Model.Category = cd.Category;
//        foreach (var item in cd.Data)
//        {
//            var data = Model.Data.FirstOrDefault(c => c.Name == item.Name);
//            if (data is not null)
//            {
//                data.Value = item.Value;
//            }
//        }

//        foreach (var item in Middlewares)
//        {
//            item.Selected = cd.Middlewares.Contains(item.Value);
//        }
//    }
//    /// <summary>
//    /// 载入步骤或流程
//    /// </summary>
//    /// <param name="category"></param>
//    /// <param name="name"></param>
//    /// <returns></returns>
//    /// <exception cref="System.Exception"></exception>
//    public async Task Load(string category, string name)
//    {
//        var flow = await _flowManager.GetStepDefinitionAsync(category, name);
//        if (flow is not null)
//        {
//            if (flow is FlowDefinition)
//            {
//                Type = DefinitionType.Flow;
//            }
//            else
//            {
//                Type = DefinitionType.Step;
//            }
//            SetStepDefinition(flow);
//        }
//        else
//        {
//            throw new Exception("未找到步骤");
//        }
//    }
//    /// <summary>
//    /// 创建新配置时载入所有步骤和配置
//    /// </summary>
//    /// <returns></returns>
//    public void Load()
//    {
//        foreach (var item in _flowMakerOption.Group)
//        {
//            StepGroups.Add(item.Key);
//        }
//        foreach (var item in _flowManager.LoadFlowCategories())
//        {
//            StepGroups.Add(item);
//        }
//    }

//    public void SetStepDefinitions()
//    {
//        StepDefinitions.Clear();

//        if (string.IsNullOrEmpty(Model.FlowCategory))
//        {
//            return;
//        }
//        if (_flowMakerOption.Group.TryGetValue(Model.FlowCategory, out var group))
//        {
//            foreach (var item in group.StepDefinitions)
//            {
//                StepDefinitions.Add(item.Name);
//            }
//        }
//        else
//        {
//            foreach (var item in _flowManager.LoadFlows(Model.FlowCategory))
//            {
//                StepDefinitions.Add(item.Name);
//            }
//        }
//    }

//    public async Task SetStepInputAsync()
//    {
//        if (string.IsNullOrEmpty(Model.FlowCategory) || string.IsNullOrEmpty(Model.FlowName))
//        {
//            return;
//        }

//        var stepDef = await _flowManager.GetStepDefinitionAsync(Model.FlowCategory, Model.FlowName);
//        if (stepDef is null)
//        {
//            return;
//        }
//        Model.Data.Clear();
//        foreach (var item in stepDef.Data)
//        {
//            var data = new InputDataViewModel(item.Name, item.DisplayName, item.Type, item.DefaultValue);
//            if (!string.IsNullOrWhiteSpace(item.OptionProviderName))
//            {
//                var pp = _serviceProvider.GetKeyedService<IOptionProviderInject>(item.Type + ":" + item.OptionProviderName);
//                if (pp is not null)
//                {
//                    var options = await pp.GetOptions();
//                    foreach (var option in options)
//                    {
//                        data.Options.Add(new FlowStepOptionViewModel(option.Value, option.Name));
//                    }
//                }
//            }
//            else
//            {
//                foreach (var option in item.Options)
//                {
//                    data.Options.Add(new FlowStepOptionViewModel(option.Name, option.DisplayName));
//                }
//            }

//            if (data.Options.Count != 0)
//            {
//                data.HasOption = true;
//            }
//            Model.Data.Add(data);
//        }
//        Type = stepDef is FlowDefinition ? DefinitionType.Flow : DefinitionType.Step;
//    }

//    [Reactive]
//    public bool EditFlow { get; set; }
//    [Reactive]
//    public ObservableCollection<string> StepGroups { get; set; } = [];
//    [Reactive]
//    public ObservableCollection<string> StepDefinitions { get; set; } = [];
//    public ReactiveCommand<bool?, Unit> SaveOrRunCommand { get; set; }
//    public async Task SaveOrRun(bool? run)
//    {
//        if (string.IsNullOrEmpty(Model.Category) || string.IsNullOrEmpty(Model.Name) || string.IsNullOrEmpty(Model.FlowCategory) || string.IsNullOrEmpty(Model.FlowName))
//        {
//            return;
//        }
//        ConfigDefinition configDefinition = new()
//        {
//            Category = Model.Category,
//            FlowCategory = Model.FlowCategory,
//            FlowName = Model.FlowName,
//            ConfigName = Model.Name,
//            ErrorHandling = Model.ErrorHandling,
//            Repeat = Model.Repeat,
//            Retry = Model.Retry,
//        };
//        foreach (var item in Model.Data)
//        {
//            if (string.IsNullOrEmpty(item.Value))
//            {
//                return;
//            }
//            configDefinition.Data.Add(new NameValue(item.Name, item.Value));
//        }
//        foreach (var item in Middlewares)
//        {
//            if (item.Selected)
//            {
//                configDefinition.Middlewares.Add(item.Value);
//            }
//        }
//        await _flowManager.SaveConfig(configDefinition);
//        if (run.HasValue && run.Value)
//        {
//            var vm = Navigate<FlowMakerMonitorViewModel>(HostScreen);
//            //await vm.Load(configDefinition.FlowCategory, configDefinition.FlowName);

//            await _messageBoxManager.Window.Handle(new ModalInfo("监控", vm) { OnlyOne = true });
//            await _flowManager.Run(configDefinition);
//        }
//        else
//        {
//            CloseModal(true);
//        }
//    }
//    [Reactive]
//    public DefinitionType Type { get; set; }
//    [Reactive]
//    public ConfigDefinitionViewModel Model { get; set; } = new();

//    public async Task Run()
//    {
//        switch (Type)
//        {
//            case DefinitionType.Step:
//                Model.Category = "Step";
//                Model.Name = "Step";
//                break;
//            case DefinitionType.Flow:
//                break;
//            case DefinitionType.Config:
//                break;
//            default:
//                break;
//        }
//        if (string.IsNullOrEmpty(Model.Category))
//        {
//            return;
//        }
//        if (string.IsNullOrEmpty(Model.Name))
//        {
//            return;
//        }
//        if (string.IsNullOrEmpty(Model.FlowCategory))
//        {
//            return;
//        }
//        if (string.IsNullOrEmpty(Model.FlowName))
//        {
//            return;
//        }
//        ConfigDefinition configDefinition = new()
//        {
//            Category = Model.Category,
//            ConfigName = Model.Name,
//            FlowCategory = Model.FlowCategory,
//            FlowName = Model.FlowName,
//            ErrorHandling = Model.ErrorHandling,
//            Repeat = Model.Repeat,
//            Retry = Model.Retry,
//            Timeout = Model.Timeout,
//        };

//        foreach (var item in Model.Data)
//        {
//            if (string.IsNullOrEmpty(item.Value))
//            {
//                return;
//            }
//            configDefinition.Data.Add(new NameValue(item.Name, item.Value));
//        }

//        await _flowManager.Run(configDefinition);
//    }
//}


//public class ConfigDefinitionViewModel : ReactiveObject
//{
//    [Reactive]
//    public string? Category { get; set; }
//    [Reactive]
//    public string? Name { get; set; }
//    /// <summary>
//    /// 流程的类别
//    /// </summary>
//    [Reactive]
//    public string? FlowCategory { get; set; }
//    /// <summary>
//    /// 流程的名称
//    /// </summary>
//    [Reactive]
//    public string? FlowName { get; set; }
//    /// <summary>
//    /// 重试
//    /// </summary>
//    [Reactive]
//    public int Retry { get; set; }
//    /// <summary>
//    /// 重复,如果是负数，则一直重复
//    /// </summary>
//    [Reactive]
//    public int Repeat { get; set; }

//    [Reactive]
//    public int Timeout { get; set; }
//    /// <summary>
//    /// 出现错误时处理方式
//    /// </summary>
//    [Reactive]
//    public ErrorHandling ErrorHandling { get; set; }
//    [Reactive]
//    public ObservableCollection<InputDataViewModel> Data { get; set; } = [];
//}

//public class InputDataViewModel(string name, string displayName, string type, string? value = null) : ReactiveObject
//{
//    [Reactive]
//    public string Type { get; set; } = type;
//    [Reactive]
//    public string Name { get; set; } = name;
//    /// <summary>
//    /// 显示名称，描述
//    /// </summary>
//    [Reactive]
//    public string DisplayName { get; set; } = displayName;

//    [Reactive]
//    public string? Value { get; set; } = value;
//    [Reactive]
//    public bool HasOption { get; set; }
//    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = [];
//}


//public class FlowStepRuntimeViewModel(Guid id, string displayName, Guid? subFlowId = null)
//{
//    public Guid Id { get; set; } = id;
//    public Guid? SubFlowId { get; set; } = subFlowId;
//    public string DisplayName { get; set; } = displayName;
//    public ObservableCollection<FlowStepRuntimeViewModel> Children { get; set; } = [];
//}

//public class MiddlewareSelectViewModel(string name, string value) : ReactiveObject
//{
//    [Reactive]
//    public string Name { get; set; } = name;

//    [Reactive]
//    public string Value { get; set; } = value;
//    [Reactive]
//    public bool Selected { get; set; }
//}