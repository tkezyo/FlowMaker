using System.Diagnostics.CodeAnalysis;
using Ty;

namespace FlowMaker;

public interface IStepDefinition
{
    string Category { get; set; }
    string Name { get; set; }
    List<DataDefinition> Data { get; set; }
}
public interface IFlowDefinition : IStepDefinition
{
    List<FlowStep> Steps { get; set; }
}
public class FlowDefinition : StepDefinition, IFlowDefinition
{
    public Guid? Id { get; set; }
    public FlowDefinition()
    {

    }
    [SetsRequiredMembers]
    public FlowDefinition(string category, string name) : base(category, name) { }

    public List<FlowStep> Steps { get; set; } = [];
}


public class StepDefinition : IStepDefinition
{
    public StepDefinition()
    {

    }

    public required string Category { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    [SetsRequiredMembers]
    public StepDefinition(string category, string name)
    {
        Category = category;
        Name = name;
    }

    public List<DataDefinition> Data { get; set; } = [];
}



public class DataDefinition(string name, string displayName, FlowDataType type, string? defaultValue = null)
{
    public FlowDataType Type { get; set; } = type;
    public string Name { get; set; } = name;
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string DisplayName { get; set; } = displayName;

    public string? DefaultValue { get; set; } = defaultValue;
    public bool IsInput { get; set; }
    public bool IsOutput { get; set; }
    public bool IsArray { get; set; }
    public int Rank { get; set; }

    public Guid? FromStepId { get; set; }
    public string? FromStepPropName { get; set; }

    public string? OptionProviderName { get; set; }
    public List<OptionDefinition> Options { get; set; } = [];
}

public enum FlowDataType
{
    Number,
    String,
    Boolean,
    DateTime,
    DateOnly,
    TimeOnly,
}

public class OptionDefinition(string displayName, string name)
{
    public string Name { get; set; } = name;
    public string DisplayName { get; set; } = displayName;
}

public class ConfigDefinition(string category, string name)
{
    public Guid? Id { get; set; }
    public Guid FlowId { get; set; }

    public string Category { get; set; } = category;

    public string Name { get; set; } = name;
    public string? ConfigName { get; set; }
    /// <summary>
    /// 重试
    /// </summary>
    public int Retry { get; set; } = 0;
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    public int Repeat { get; set; } = 1;
    public int Timeout { get; set; }
    public string? LogView { get; set; }
    public bool ErrorStop { get; set; }

    public List<NameValue> Data { get; set; } = [];
    public List<string> FlowMiddlewares { get; set; } = [];
    public List<string> StepGroupMiddlewares { get; set; } = [];
    public List<string> StepMiddlewares { get; set; } = [];
}