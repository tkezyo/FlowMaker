using DynamicData;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FlowMaker;

public class FlowContext
{
    /// <summary>
    /// 父流程Id
    /// </summary>
    public Guid[] FlowIds { get; }
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; }
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }


    public SourceList<EventLog> Events { get; set; } = new();

    public bool Finally { get; set; }

    public ConfigDefinition ConfigDefinition { get; set; }
    public ConcurrentDictionary<string, string?> EventData { get; set; } = [];
    public List<string> Middlewares { get; set; } = [];
    /// <summary>
    /// 所有步骤的状态
    /// </summary>
    public SourceCache<StepStatus, Guid> StepState { get; protected set; } = new(c => c.StepId);
    /// <summary>
    /// 所有的全局变量
    /// </summary>
    public SourceCache<FlowGlobeData, string> Data { get; set; } = new(c => c.Name);

    public FlowContext(ConfigDefinition configDefinition, Guid[] flowIds, int currentIndex, int errorIndex)
    {
        ConfigDefinition = configDefinition;
        FlowIds = flowIds;
        CurrentIndex = currentIndex;
        ErrorIndex = errorIndex;
        StartTime = DateTime.Now;
    }


}

public class StepContext(FlowStep step, FlowContext flowContext, StepOnceStatus stepOnceStatus)
{

    public FlowStep Step { get; } = step;
    public FlowContext FlowContext { get; } = flowContext;
    public int CurrentIndex { get; } = stepOnceStatus.CurrentIndex;
    public int ErrorIndex { get; } = stepOnceStatus.ErrorIndex;
    public StepOnceStatus StepOnceStatus { get; } = stepOnceStatus;

    public void Log(string log, LogLevel logLevel = LogLevel.Information)
    {
        StepOnceStatus.Log(log, logLevel);
    }
}

public class StepOnceStatus(int currentIndex, int errorIndex)
{
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; } = currentIndex;
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; } = errorIndex;

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }
    /// <summary>
    /// 输入
    /// </summary>
    public List<NameValue> Inputs { get; set; } = [];
    /// <summary>
    /// 输出
    /// </summary>
    public List<NameValue> Outputs { get; set; } = [];
    public StepOnceState State { get; set; }
    /// <summary>
    /// 日志
    /// </summary>
    public SourceList<LogInfo> Logs { get; set; } = new SourceList<LogInfo>();

    public void Log(string log, LogLevel logLevel = LogLevel.Information)
    {
        var info = new LogInfo(log, logLevel, DateTime.Now);
        Logs.Add(info);
    }

    /// <summary>
    /// 附加属性
    /// </summary>
    public Dictionary<string, object> ExtraData { get; set; } = [];
}

public record LogInfo(string Log, LogLevel LogLevel, DateTime Time);
public class StepStatus
{
    public Guid StepId { get; set; }
    public StepState State { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }
    /// <summary>
    /// 需要等待的事件
    /// </summary>
    public List<string> Waits { get; set; } = [];
    public SourceCache<StepOnceStatus, string> OnceLogs { get; set; } = new SourceCache<StepOnceStatus, string>(c => c.CurrentIndex + "." + c.ErrorIndex);
}


public class NameValue(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
}

public class FlowGlobeData(string name, string type, string? value = null)
{
    public bool IsInput { get; set; }
    public bool IsOutput { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = name;
    /// <summary>
    /// 类型
    /// </summary>
    public string Type { get; set; } = type;
    /// <summary>
    /// 值
    /// </summary>
    public string? Value { get; set; } = value;
}

public class EventLog
{
    public DateTime Time { get; set; }
    public required string EventName { get; set; }
    public string? EventData { get; set; }
}


/// <summary>
/// 步骤运行状态
/// </summary>
public enum FlowState
{
    None,
    Running,
    Complete,
    Cancel,
    Error,
}
public enum StepState
{
    Wait,
    Start,
    Complete,
    Error,
}

public enum StepOnceState
{
    Wait,
    Start,
    Complete,
    Error,
    Skip
}
/// <summary>
/// 触发步骤的事件
/// </summary>
public class ExecuteStepEvent
{
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
    public string? EventData { get; set; }
}
/// <summary>
/// 事件类型
/// </summary>
public enum EventType
{
    PreStep,
    Event,
    StartFlow,
}

