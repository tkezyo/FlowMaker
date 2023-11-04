namespace FlowMaker.Models;

public class FlowDefinition
{
    /// <summary>
    /// 同一个流程里可以保护多个子流程
    /// </summary>
    public required string Category { get; set; }
    /// <summary>
    /// 子流程名称,如果为空代表是主流程
    /// </summary>
    public string? Name { get; set; }

    public List<FlowStep> Steps { get; set; } = new();
    public List<FlowStep> CompensateSteps { get; set; } = new();
    public List<FlowInput> ExcuteCheckers { get; set; } = new();

    public List<StepInputDefinition> Inputs { get; set; } = new();
    public List<StepOutputDefinition> Outputs { get; set; } = new();

}
public class StepDefinition
{
    public required string Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string DisplayName { get; set; }
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public List<StepInputDefinition> Inputs { get; set; } = new();
    public List<StepOutputDefinition> Outputs { get; set; } = new();
}
public class ConverterDefinition
{
    public required string Category { get; set; }
    public required string DisplayName { get; set; }
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public List<StepInputDefinition> Inputs { get; set; } = new();
    public required string Output { get; set; }
}
public class StepOutputDefinition
{
    public string Name { get; set; }

    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string DisplayName { get; set; }
    public string Type { get; set; }

    public StepOutputDefinition(string name, string displayName, string type)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;
    }
}

public class StepInputDefinition
{
    public string Type { get; set; }
    public string Name { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string DisplayName { get; set; }

    public string? DefaultValue { get; set; }


    public List<OptionDefinition> Options { get; set; }


    public StepInputDefinition(string name, string displayName, string type, string? defaultValue = null)
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

    public OptionDefinition(string name, string value)
    {
        Name = name;
        DisplayName = value;
    }
}

public enum InputDisplayType
{
    Text,
    ComboBox,
    CheckBox,
}

