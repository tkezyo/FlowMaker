using System.Collections.Concurrent;

namespace FlowMaker.Models;

public class FlowContext
{
    /// <summary>
    /// 父流程Id
    /// </summary>
    public Guid[] FlowIds { get; set; } = [];
    /// <summary>
    /// 流程配置
    /// </summary>
    public FlowDefinition FlowDefinition { get; }

    public FlowContext(FlowDefinition flowDefinition)
    {
        FlowDefinition = flowDefinition;
        InitExecuteStepIds();
    }
    public void InitExecuteStepIds()
    {
        ExcuteStepIds.Clear();
        foreach (var item in FlowDefinition.Steps)
        {
            if (item.WaitEvents.Count == 0)
            {
                if (!ExcuteStepIds.TryGetValue(EventType.StartFlow.ToString(), out var list))
                {
                    list = [];
                    ExcuteStepIds.Add(EventType.StartFlow.ToString(), list);
                }
                list.Add(item.Id);
                continue;
            }
            foreach (var input in item.Inputs)
            {
                void register(FlowInput flowInput)
                {
                    if (input.Mode == InputMode.Event)
                    {
                        var key = flowInput.Mode + flowInput.Value;
                        if (!ExcuteStepIds.TryGetValue(key, out var list))
                        {
                            list = [];
                            ExcuteStepIds.Add(key, list);
                        }
                        list.Add(flowInput.Id);
                        foreach (var subinput in flowInput.Inputs)
                        {
                            register(input);
                        }
                    }
                }
                register(input);
            }
            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.Step => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.Debug => item.Id.ToString(),
                    EventType.StartFlow => "",
                    _ => ""
                };

                if (!ExcuteStepIds.TryGetValue(key, out var list))
                {
                    list = [];
                    ExcuteStepIds.Add(key, list);
                }
                list.Add(item.Id);
            }
        }
    }
    /// <summary>
    /// 初始化状态
    /// </summary>
    public void InitState()
    {
        StepState.Clear();
        foreach (var item in FlowDefinition.Steps)
        {
            var state = new StepStatus();

            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.Step => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.Debug => item.Id.ToString() + "Debug",
                    EventType.StartFlow => "",
                    _ => ""
                };
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }
                state.Waits.Add(key);
            }
            StepState.Add(item.Id, state);
        }
    }

    /// <summary>
    /// 所有步骤的状态
    /// </summary>
    public Dictionary<Guid, StepStatus> StepState { get; protected set; } = [];
    /// <summary>
    /// 触发某事件时需要执行的Step
    /// </summary>
    public Dictionary<string, List<Guid>> ExcuteStepIds { get; } = [];
    /// <summary>
    /// 所有的全局变量
    /// </summary>
    public ConcurrentDictionary<string, FlowGlobeData> Data { get; set; } = new();
}

public class StepContext(FlowStep step, StepStatus status, StepOnceStatus stepOnceStatus)
{
    public FlowStep Step { get; } = step;
    public StepStatus Status { get; set; } = status;
    public StepOnceStatus StepOnceStatus { get; } = stepOnceStatus;
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
}

public class StepStatus
{
    /// <summary>
    /// 已完成
    /// </summary>
    public bool Complete { get; set; }
    /// <summary>
    /// 已开始
    /// </summary>
    public bool Started { get; set; }
    /// <summary>
    /// 暂停
    /// </summary>
    public bool Suspend { get; set; }
    public StepState State { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }
    /// <summary>
    /// 需要等待的事件
    /// </summary>
    public List<string> Waits { get; set; } = [];
    public List<StepOnceStatus> OnceStatuses { get; set; } = [];
}


public class NameValue(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
}

public class FlowGlobeData(string name, string type, string value)
{
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
    public string Value { get; set; } = value;
}