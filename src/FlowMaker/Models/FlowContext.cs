using DynamicData;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using Ty;

namespace FlowMaker;

public class FlowContext(IFlowDefinition flowDefinition, ConfigDefinition configDefinition, List<FlowInput> checkers, Guid[] flowIds, int currentIndex, int errorIndex, string? parentIndex, SourceList<LogInfo>? logger = null, SourceCache<WaitEvent, string>? waitEvents = null, SourceCache<FlowGlobeData, string>? data = null, SourceList<EventLog>? eventLog = null) : IDisposable
{
    public string Id { get; set; } = string.Join(",", flowIds);
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
    public string Index { get; set; } = $"{(string.IsNullOrEmpty(parentIndex) ? null : parentIndex + ",")}{currentIndex}.{errorIndex}";
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
    public List<string> FlowMiddlewares { get; set; } = [];
    public List<string> StepGroupMiddlewares { get; set; } = [];
    public List<string> StepMiddlewares { get; set; } = [];

    public ConfigDefinition ConfigDefinition { get; set; } = configDefinition;

    /// <summary>
    /// 事件数据
    /// </summary>
    public ConcurrentDictionary<string, string?> EventData { get; set; } = [];
    /// <summary>
    /// 事件记录
    /// </summary>
    public SourceList<EventLog> EventLogs { get; set; } = eventLog ?? new();
    /// <summary>
    /// 等待中的事件
    /// </summary>
    public SourceCache<WaitEvent, string> WaitEvents { get; set; } = waitEvents ?? new(c => c.Name);
    /// <summary>
    /// 所有步骤的状态
    /// </summary>
    public SourceCache<StepGroupStatus, Guid> StepState { get; set; } = new(c => c.StepId);
    /// <summary>
    /// 所有的全局变量
    /// </summary>
    public SourceCache<FlowGlobeData, string> Data { get; set; } = data ?? new(c => c.Name);

    public SourceList<LogInfo> Logs { get; set; } = logger ?? new();


    public IFlowDefinition FlowDefinition { get; set; } = flowDefinition;
    /// <summary>
    /// 检查项
    /// </summary>
    public List<FlowInput> Checkers { get; set; } = checkers;

    /// <summary>
    /// 触发某事件时需要执行的Step
    /// </summary>
    public Dictionary<string, List<Guid>> ExecuteStepIds { get; } = [];

    /// <summary>
    /// 执行步骤的事件
    /// </summary>
    public Subject<ExecuteStepEvent> ExecuteStepSubject { get; } = new();
    public TaskCompletionSource TaskCompletionSource { get; set; } = new();
    /// <summary>
    /// 执行结果
    /// </summary>
    public FlowResult Result { get; set; } = new FlowResult(currentIndex, errorIndex);



    /// <summary>
    /// 流程状态
    /// </summary>
    public FlowState State { get; set; } = FlowState.Wait;

    private void InitExecuteStepIds()
    {
        ExecuteStepIds.Clear();
        if (FlowDefinition is EmbeddedFlowDefinition embeddedFlowDefinition)
        {
            //遍历子流程，如果是串行执行，则在他的WaitEvents中添加依赖的流程Id，如果是并行执行，则在他的WaitEvents中添加上一个串行流程Id
            FlowStep? last = null;
            foreach (var item in FlowDefinition.Steps)
            {
                if (last is not null)
                {
                    item.WaitEvents.Add(new FlowEvent
                    {
                        Type = EventType.PreStep,
                        StepId = last?.Id
                    });
                }
                if (!item.Parallel)
                {
                    last = item;
                }
            }
        }

        foreach (var item in FlowDefinition.Steps)
        {
            List<string> waitEvent = [];

            void Register(FlowInput flowInput)
            {
                if (flowInput.Mode == InputMode.Event)
                {
                    if (string.IsNullOrEmpty(flowInput.Value))
                    {
                        return;
                    }
                    var key = EventType.Event + flowInput.Value;
                    waitEvent.Add(key);
                    if (!ExecuteStepIds.TryGetValue(key, out var list))
                    {
                        list = [];
                        ExecuteStepIds.Add(key, list);
                    }
                    WaitEvents.AddOrUpdate(new WaitEvent(flowInput.Value, true));
                    list.Add(item.Id);
                    foreach (var subInput in flowInput.Inputs)
                    {
                        Register(subInput);
                    }
                }
            }
            foreach (var wait in item.Ifs.Keys.Union(item.AdditionalConditions.Keys))
            {
                var checker = Checkers.FirstOrDefault(c => c.Id == wait) ?? item.Checkers.FirstOrDefault(c => c.Id == wait);
                if (checker is null)
                {
                    continue;
                }
                Register(checker);
            }

            foreach (var input in item.Inputs)
            {
                Register(input);
            }
            Register(item.TimeOut);
            Register(item.Repeat);
            Register(item.Retry);
            Register(item.ErrorHandling);
            Register(item.Finally);


            foreach (var wait in item.WaitEvents)
            {
                if (wait.Type == EventType.Event)
                {
                    if (string.IsNullOrEmpty(wait.EventName))
                    {
                        continue;
                    }
                    WaitEvents.AddOrUpdate(new WaitEvent(wait.EventName, false));
                }

                var key = wait.Type + wait.Type switch
                {
                    EventType.PreStep => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.StartFlow => string.Empty,
                    _ => string.Empty
                };
                waitEvent.Add(key);

                if (!ExecuteStepIds.TryGetValue(key, out var list))
                {
                    list = [];
                    ExecuteStepIds.Add(key, list);
                }
                list.Add(item.Id);
            }

            if (waitEvent.Count == 0)
            {
                if (!ExecuteStepIds.TryGetValue(EventType.StartFlow.ToString(), out var list))
                {
                    list = [];
                    ExecuteStepIds.Add(EventType.StartFlow.ToString(), list);
                }
                list.Add(item.Id);
                continue;
            }
        }
    }

    public CompositeDisposable Disposables { get; set; } = [];

    /// <summary>
    /// 初始化状态
    /// </summary>
    public void Init()
    {
        InitExecuteStepIds();
        StepState.Clear();

        foreach (var item in FlowDefinition.Steps)
        {
            var state = new StepGroupStatus
            {
                StepId = item.Id
            };

            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.PreStep => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.StartFlow => "",
                    _ => ""
                };
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                state.Waits.Add(key);
            }
            state.Waits = state.Waits.Distinct().ToList();
            StepState.AddOrUpdate(state);
        }


        foreach (var item in ConfigDefinition.FlowMiddlewares)
        {
            if (!FlowMiddlewares.Any(c => c == item))
            {
                FlowMiddlewares.Add(item);
            }
        }
        foreach (var item in ConfigDefinition.StepGroupMiddlewares)
        {
            if (!StepGroupMiddlewares.Any(c => c == item))
            {
                StepGroupMiddlewares.Add(item);
            }
        }
        foreach (var item in ConfigDefinition.StepMiddlewares)
        {
            if (!StepMiddlewares.Any(c => c == item))
            {
                StepMiddlewares.Add(item);
            }
        }

        foreach (var item in FlowDefinition.Data)//写入 globe data
        {
            var data = Data.Lookup(item.Name);
            if (!data.HasValue)
            {
                var value = ConfigDefinition.Data.FirstOrDefault(c => c.Name == item.Name);
                var globeData = new FlowGlobeData(item.Name, item.Type, value?.Value)
                {
                    IsInput = item.IsInput,
                    IsOutput = item.IsOutput
                };
                Data.AddOrUpdate(globeData);
            }
            else
            {
                var value = ConfigDefinition.Data.FirstOrDefault(c => c.Name == item.Name);
                data.Value.Value = value?.Value;
                Data.AddOrUpdate(data.Value);
            }
        }


        var d = EventLogs.Connect().SubscribeMany(c =>
          {
              //获取添加的事件，如果有等待的事件，则触发
              if (WaitEvents.Items.Any(v => v.Name == c.EventName))
              {
                  EventData[c.EventName] = c.EventData;
                  ExecuteStepSubject.OnNext(new ExecuteStepEvent
                  {
                      Type = EventType.Event,
                      EventData = c.EventData,
                      EventName = c.EventName,
                      Context = this
                  });

                  WaitEvents.RemoveKey(c.EventName);
              }
              return Disposable.Empty;

          }).Subscribe();
        Disposables.Add(d);

        Disposables.Add(EventLogs);
        Disposables.Add(WaitEvents);
        Disposables.Add(Data);
        Disposables.Add(Logs);
    }

    public void Dispose()
    {

        foreach (var item in StepState.Items)
        {
            //item.OnceLogs.Clear();
            item.OnceLogs.Dispose();
        }

        StepState.Dispose();
        Disposables.Dispose();
    }
}
public record StepGroupContext(FlowContext FlowContext, FlowStep Step, StepGroupStatus Status);

public class StepContext(FlowStep step, FlowContext flowContext, StepGroupStatus stepGroupStatus, StepStatus stepStatus)
{
    public FlowStep Step { get; } = step;
    public FlowContext FlowContext { get; } = flowContext;
    public int CurrentIndex { get; } = stepStatus.CurrentIndex;
    public int ErrorIndex { get; } = stepStatus.ErrorIndex;
    public StepStatus StepStatus { get; } = stepStatus;
    public StepGroupStatus StepGroupStatus { get; } = stepGroupStatus;

    public void Log(string log, LogLevel logLevel = LogLevel.Information)
    {
        StepStatus.Log(log, logLevel);
    }
}

public class StepStatus(int currentIndex, int errorIndex, string parentIndex, Action<StepStatus, string, LogLevel> logAction, Action<StepStatus> update)
{
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; } = currentIndex;
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; } = errorIndex;
    public string Index { get; set; } = $"{parentIndex}-{currentIndex}.{errorIndex}";

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
    /// 附加属性
    /// </summary>
    public Dictionary<string, object> ExtraData { get; set; } = [];
    protected Action<StepStatus, string, LogLevel> LogAction { get; set; } = logAction;

    public Action<StepStatus> Update { get; set; } = update;
    public void Log(string log, LogLevel logLevel = LogLevel.Information)
    {
        LogAction.Invoke(this, log, logLevel);
    }

    public const string AdditionalConditions = "AdditionalConditions";
}

public record LogInfo(string Log, LogLevel LogLevel, DateTime Time, Guid StepId, string Index);
public class StepGroupStatus
{
    public Guid StepId { get; set; }
    public StepState State { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int Repeat { get; set; }
    public int Retry { get; set; }
    public bool Finally { get; set; }
    public ErrorHandling ErrorHandling { get; set; }
    /// <summary>
    /// 需要等待的事件
    /// </summary>
    public List<string> Waits { get; set; } = [];
    public SourceCache<StepStatus, string> OnceLogs { get; set; } = new(c => $"{c.CurrentIndex}.{c.ErrorIndex}");
}



public record class FlowGlobeData(string Name, string Type, string? Value = null, bool IsInput = false, bool IsOutput = false)
{
    public string? Value { get; set; } = Value;
}

public record EventLog(DateTime Time, string EventName, string? EventData);

public record WaitEvent(string Name, bool NeedData);


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
    Skip,
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
    public required FlowContext Context { get; set; }
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

