﻿using Ty;

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
    public bool GanttMode { get; set; }
    public List<FlowStep> Steps { get; set; } = [];
    public List<FlowInput> Checkers { get; set; } = [];

    public List<EmbeddedFlowDefinition> EmbeddedFlows { get; set; } = [];
}

public class EmbeddedFlowDefinition : StepDefinition, IFlowDefinition
{
    public Guid StepId { get; set; }
    public List<FlowStep> Steps { get; set; } = [];
}

/// <summary>
/// 流程文件信息
/// </summary>
public class FlowDefinitionFileInfo
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public required DateTime CreationTime { get; set; }
    public required DateTime ModifyTime { get; set; }
    public List<string> Configs { get; set; } = [];
}

public class StepDefinition : IStepDefinition
{
    public required string Category { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    public List<DataDefinition> Data { get; set; } = [];
}


public class ConverterDefinition
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public List<DataDefinition> Inputs { get; set; } = [];
    public required string Output { get; set; }
}
public class DataDefinition(string name, string displayName, string type, string? defaultValue = null)
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
    public bool IsArray { get; set; }
    public int Rank { get; set; }
    /// <summary>
    /// 数组的类型
    /// </summary>
    public string? SubType { get; set; }

    public Guid? FromStepId { get; set; }
    public string? FromStepPropName { get; set; }

    public string? OptionProviderName { get; set; }
    public List<OptionDefinition> Options { get; set; } = [];
}

public class OptionDefinition(string displayName, string name)
{
    public string Name { get; set; } = name;
    public string DisplayName { get; set; } = displayName;
}

public class ConfigDefinition
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public string? ConfigName { get; set; }
    /// <summary>
    /// 重试
    /// </summary>
    public int Retry { get; set; }
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    public int Repeat { get; set; }
    public int Timeout { get; set; }
    public string? LogView { get; set; }
    public bool ErrorStop { get; set; }

    public List<NameValue> Data { get; set; } = [];
    public List<string> Middlewares { get; set; } = [];
}