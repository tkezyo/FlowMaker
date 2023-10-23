using System.Diagnostics.CodeAnalysis;

namespace FlowMaker.Models;

public class FlowDefinition
{
    public List<FlowStep> Steps { get; set; } = new();
    public List<StepInputDefinition> Inputs { get; set; } = new();
    public List<StepOutputDefinition> Outputs { get; set; } = new();
 
}
public class StepDefinition
{
    /// <summary>
    /// 名称
    /// </summary>
    public required string DiaplayName { get; set; }
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public List<StepInputDefinition> Inputs { get; set; } = new();
    public List<StepOutputDefinition> Outputs { get; set; } = new();
}
public class ConverterDefinition
{
    public required string DiaplayName { get; set; }
    public string Name { get; set; }
    public required Type Type { get; set; }
    public required string To { get; set; }
    public List<StepInputDefinition> Inputs { get; set; } = new();
    [SetsRequiredMembers]
    public ConverterDefinition(string name, string diaplayName, string to, Type type)
    {
        Name = name;
        To = to;
        Type = type;
        DiaplayName = diaplayName;
    }
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

    public InputDisplayType DisplayType { get; set; }

    public StepInputDefinition(string name, string displayName, string type)
    {
        Name = name;
        Options = new List<OptionDefinition>();
        DisplayType = InputDisplayType.Text;
        DisplayName = displayName;
        Type = type;
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

