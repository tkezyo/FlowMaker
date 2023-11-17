namespace FlowMaker.Models;

public interface IStepDefinition
{
    string Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    string DisplayName { get; }
    string Name { get; set; }
    List<StepDataDefinition> Datas { get; set; }
}
public class FlowDefinition : IStepDefinition
{
    /// <summary>
    /// 同一个流程里可以保护多个子流程
    /// </summary>
    public required string Category { get; set; }
    /// <summary>
    /// 子流程名称,如果为空代表是主流程
    /// </summary>
    public required string Name { get; set; }
    public string DisplayName => Name;

    public List<FlowStep> Steps { get; set; } = [];
    public List<FlowInput> Checkers { get; set; } = [];

    public List<StepDataDefinition> Datas { get; set; } = [];
}
public class StepDefinition : IStepDefinition
{
    public required string Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string DisplayName { get; set; }
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public List<StepDataDefinition> Datas { get; set; } = [];
}
public class ConverterDefinition
{
    public required string Category { get; set; }
    public required string DisplayName { get; set; }
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public List<StepDataDefinition> Inputs { get; set; } = [];
    public required string Output { get; set; }
}
public class StepDataDefinition
{
    public string Type { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string DisplayName { get; set; }

    public string? DefaultValue { get; set; }
    public bool IsInput { get; set; }
    public bool IsOutput { get; set; }

    public Guid? FromStepId { get; set; }
    public string? FromStepPropName { get; set; }


    public List<OptionDefinition> Options { get; set; }


    public StepDataDefinition(string name, string displayName, string type, string? defaultValue = null)
    {
        Name = name;
        Options = new List<OptionDefinition>();
        DisplayName = displayName;
        Type = type;
        DefaultValue = defaultValue;
    }
}

public class OptionDefinition
{
    public string Name { get; set; }
    public string DisplayName { get; set; }

    public OptionDefinition(string displayName, string name)
    {
        Name = displayName;
        DisplayName = name;
    }
}

public enum InputDisplayType
{
    Text,
    ComboBox,
    CheckBox,
}

