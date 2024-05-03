using DynamicData;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FlowMaker;

public class FlowContext(ConfigDefinition configDefinition, Guid[] flowIds, int currentIndex, int errorIndex)
{
    /// <summary>
    /// 流程Id
    /// </summary>
    public Guid[] FlowIds { get; } = flowIds;
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; } = currentIndex;
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; } = errorIndex;
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;
    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
    /// <summary>
    /// 已经出错，不再执行，直接跳过到Finally步骤
    /// </summary>
    public bool Finally { get; set; }
    /// <summary>
    /// 使用的中间件
    /// </summary>
    public List<string> Middlewares { get; set; } = [];

    public ConfigDefinition ConfigDefinition { get; set; } = configDefinition;

    /// <summary>
    /// 事件数据
    /// </summary>
    public ConcurrentDictionary<string, string?> EventData { get; set; } = [];
    /// <summary>
    /// 事件记录
    /// </summary>
    public SourceList<EventLog> EventLogs { get; set; } = new();


    /// <summary>
    /// 所有步骤的状态
    /// </summary>
    public SourceCache<StepStatus, Guid> StepState { get; protected set; } = new(c => c.StepId);
    /// <summary>
    /// 所有的全局变量
    /// </summary>
    public SourceCache<FlowGlobeData, string> Data { get; set; } = new(c => c.Name);
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
    Wait,
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

