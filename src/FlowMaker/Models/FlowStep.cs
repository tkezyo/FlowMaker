using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace FlowMaker;

public class FlowStep
{
    public FlowStep()
    {
        TimeOut = new FlowInput("TimeOut");
        Retry = new FlowInput("Retry");
        Repeat = new FlowInput("Repeat");
        ErrorHandling = new FlowInput("ErrorHandling");
        Finally = new FlowInput("Finally");
    }
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
    /// 并行执行-仅在非甘特图模式下有效
    /// </summary>
    public bool Parallel { get; set; }
    /// <summary>
    /// 子流程
    /// </summary>
    public StepType Type { get; set; }
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
    public FlowInput TimeOut { get; set; }

    /// <summary>
    /// 重试 int
    /// </summary>
    public FlowInput Retry { get; set; }
    /// <summary>
    /// 重复  int
    /// </summary>
    public FlowInput Repeat { get; set; }
    /// <summary>
    /// 出现错误时处理方式 ErrorHandling
    /// </summary>
    public FlowInput ErrorHandling { get; set; }

    /// <summary>
    /// 结束步骤
    /// </summary>
    public FlowInput Finally { get; set; }

    /// <summary>
    /// 是否可执行，同时可作为Break的条件, 可能来自于全局的 checker或自己的 checker
    /// </summary>
    public Dictionary<Guid, bool> Ifs { get; set; } = [];
    /// <summary>
    /// 附加条件,不是用来判断是否执行的，而是添加到附加属性中的
    /// </summary>
    public Dictionary<Guid, bool> AdditionalConditions { get; set; } = [];
    /// <summary>
    /// 用于Ifs的判断
    /// </summary>
    public List<FlowInput> Checkers { get; set; } = [];
    /// <summary>
    /// 等待事件
    /// </summary>
    public List<FlowEvent> WaitEvents { get; set; } = [];
    public TimeSpan Time { get; set; }
}

public class FlowEvent
{
    /// <summary>
    /// 等待的事件类型
    /// </summary>
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
    public string? EventDataType { get; set; }
}

/// <summary>
/// 步骤类型
/// </summary>
public enum StepType
{
    /// <summary>
    /// 普通步骤
    /// </summary>
    Normal,
    /// <summary>
    /// 子流程
    /// </summary>
    SubFlow,
    /// <summary>
    /// 嵌入的子流程
    /// </summary>
    Embedded,
}

public class FlowInput
{
    public FlowInput()
    {
    }
    [SetsRequiredMembers]
    public FlowInput(string name, Guid? id = null)
    {
        Name = name;
        Id = id ?? Guid.NewGuid();
    }

    public required string Name { get; set; }
    public required Guid Id { get; set; }
    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }

    public InputMode Mode { get; set; }
    public int[] Dims { get; set; } = [];
    /// <summary>
    /// Globe模式下为全局变量, 普通或选项为具体的值,Event为事件名称
    /// </summary>
    public string? Value { get; set; }
    public List<FlowInput> Inputs { get; set; } = [];
}
public enum InputMode
{
    Normal,
    Array,
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
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; set; }
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; set; }
    public bool Success { get; set; }
    public Exception? Exception { get; set; }
    public List<FlowResultData> Data { get; set; } = [];

}
public class FlowResultData
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public string? Value { get; set; }
    public required string Type { get; set; }
}
[Flags]
public enum ErrorHandling
{
    /// <summary>
    /// 跳过
    /// </summary>
    [Description("跳过")]
    Skip,
    /// <summary>
    /// 跳到Finally或停止
    /// </summary>
    [Description("终止")]
    Finally = 1,
    /// <summary>
    /// 停止
    /// </summary>
    [Description("立即停止")]
    Terminate
}


