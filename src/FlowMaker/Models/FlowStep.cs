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
    /// 输入
    /// </summary>
    public List<FlowInput> Inputs { get; set; } = new();
    /// <summary>
    /// 输出
    /// </summary>
    public List<FlowOutput> Outputs { get; set; } = new();

    /// <summary>
    /// 超时,秒
    /// </summary>
    public double TimeOut { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    public int Retry { get; set; }
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    public int Repeat { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    public ErrorHandling ErrorHandling { get; set; }

    /// <summary>
    /// 谁的回退任务,stepdId
    /// </summary>
    public Guid? Compensate { get; set; }



    /// <summary>
    /// 是否可执行，同时可作为Break的条件
    /// </summary>
    public Dictionary<Guid, bool> Ifs { get; set; } = new();
    public List<FlowInput> Checkers { get; set; } = new();
    /// <summary>
    /// 等待事件
    /// </summary>
    public List<FlowWait> WaitEvents { get; set; } = new();
}

public class FlowWait
{
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
}


public class FlowInput
{
    public required string Name { get; set; }
    public required Guid Id { get; set; }
    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }

    public InputMode Mode { get; set; }
    /// <summary>
    /// Globe模式下为全局变量, 普通或选项为具体的值,Wait为事件名称
    /// </summary>
    public string? Value { get; set; }
    public List<FlowInput> Inputs { get; protected set; } = new();
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
    public List<FlowInput> Inputs { get; protected set; } = new();
}
public enum OutputMode
{
    Drop,
    Globe,
    GlobeWithConverter,
}

public enum ErrorHandling
{
    /// <summary>
    /// 跳过
    /// </summary>
    Skip,
    /// <summary>
    /// 暂停
    /// </summary>
    Suspend,
    /// <summary>
    /// 停止
    /// </summary>
    Terminate
}


