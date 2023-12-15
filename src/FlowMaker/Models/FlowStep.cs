﻿using System.ComponentModel.DataAnnotations;

namespace FlowMaker.Models;

public class FlowStep
{
    /// <summary>
    /// 步骤唯一Id
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 显示名称
    /// </summary>
    public required string DisplayName { get; set; }
    /// <summary>
    /// 步骤的类别
    /// </summary>
    public required string Category { get; set; }
    /// <summary>
    /// 步骤的名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 子流程
    /// </summary>
    public bool IsSubFlow { get; set; }
    /// <summary>
    /// 输入
    /// </summary>
    public List<FlowInput> Inputs { get; set; } = [];
    /// <summary>
    /// 输出
    /// </summary>
    public List<FlowOutput> Outputs { get; set; } = [];

    /// <summary>
    /// 超时,秒 double
    /// </summary>
    public FlowInput TimeOut { get; set; } = new FlowInput("TimeOut");

    /// <summary>
    /// 重试 int
    /// </summary>
    public FlowInput Retry { get; set; } = new FlowInput("Retry");
    /// <summary>
    /// 重复  int
    /// </summary>
    public FlowInput Repeat { get; set; } = new FlowInput("Repeat");
    /// <summary>
    /// 出现错误时处理方式 ErrorHandling
    /// </summary>
    public FlowInput ErrorHandling { get; set; } = new FlowInput("ErrorHandling");

    /// <summary>
    /// 谁的回退任务,stepdId
    /// </summary>
    public Guid? Compensate { get; set; }



    /// <summary>
    /// 是否可执行，同时可作为Break的条件
    /// </summary>
    public Dictionary<Guid, bool> Ifs { get; set; } = [];
    public List<FlowInput> Checkers { get; set; } = [];
    /// <summary>
    /// 等待事件
    /// </summary>
    public List<FlowWait> WaitEvents { get; set; } = [];
}

public class FlowWait
{
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
}


public class FlowInput(string name, Guid? id = null)
{
    public string Name { get; set; } = name;
    public Guid Id { get; set; } = id ?? Guid.NewGuid();
    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }

    public InputMode Mode { get; set; }
    /// <summary>
    /// Globe模式下为全局变量, 普通或选项为具体的值,Wait为事件名称
    /// </summary>
    public string? Value { get; set; }
    public List<FlowInput> Inputs { get; set; } = [];
}
public enum InputMode
{
    Normal,
    Option,
    Globe,
    Converter,
    Event
}
public class FlowOutput
{
    public required string Name { get; set; }

    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }
    public string? InputKey { get; set; }

    public OutputMode Mode { get; set; }

    public string? ConvertToType { get; set; }
    public required string Type { get; set; }

    public string? GlobeDataName { get; set; }
    public List<FlowInput> Inputs { get; set; } = [];
}
public enum OutputMode
{
    Drop,
    Globe,
    GlobeWithConverter,
}

public class FlowResult
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Value { get; set; }
    public required string Type { get; set; }
}
public enum ErrorHandling
{
    /// <summary>
    /// 跳过
    /// </summary>
    Skip,
    /// <summary>
    /// 暂停,需要添加恢复事件
    /// </summary>
    Suspend,
    /// <summary>
    /// 停止
    /// </summary>
    Terminate
}


