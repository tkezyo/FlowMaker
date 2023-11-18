namespace FlowMaker.Models;

public interface IStepDefinition
{
    string Category { get; set; }
    string Name { get; set; }
    List<StepDataDefinition> Data { get; set; }
}
public class FlowDefinition : StepDefinition, IStepDefinition
{
    public List<FlowStep> Steps { get; set; } = [];
    public List<FlowInput> Checkers { get; set; } = [];
}
public class StepDefinition : IStepDefinition
{
    public required string Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    public List<StepDataDefinition> Data { get; set; } = [];
}
public class ConverterDefinition
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public List<StepDataDefinition> Inputs { get; set; } = [];
    public required string Output { get; set; }
}
public class StepDataDefinition(string name, string displayName, string type, string? defaultValue = null)
{
    public string Type { get; set; } = type;
    public string Name { get; set; } = name;
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string DisplayName { get; set; } = displayName;

    public string? DefaultValue { get; set; } = defaultValue;
    public bool IsInput { get; set; }
    public bool IsOutput { get; set; }

    public Guid? FromStepId { get; set; }
    public string? FromStepPropName { get; set; }


    public List<OptionDefinition> Options { get; set; } = [];
}

public class OptionDefinition(string displayName, string name)
{
    public string Name { get; set; } = displayName;
    public string DisplayName { get; set; } = name;
}

public enum InputDisplayType
{
    Text,
    ComboBox,
    CheckBox,
}

