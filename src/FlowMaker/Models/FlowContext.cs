﻿using System.Collections.Concurrent;

namespace FlowMaker.Models;

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
    /// <summary>
    /// 流程配置
    /// </summary>
    public FlowDefinition FlowDefinition { get; }
    public ConfigDefinition ConfigDefinition { get; set; }
    public ConcurrentDictionary<string, string?> EventData { get; set; } = [];
    public List<string> Middlewares { get; set; } = [];
    public FlowContext(FlowDefinition flowDefinition, ConfigDefinition configDefinition, Guid[] flowIds, int currentIndex, int errorIndex)
    {
        FlowDefinition = flowDefinition;
        ConfigDefinition = configDefinition;
        FlowIds = flowIds;
        CurrentIndex = currentIndex;
        ErrorIndex = errorIndex;
        InitExecuteStepIds();
    }
    public void InitExecuteStepIds()
    {
        ExecuteStepIds.Clear();
        foreach (var item in FlowDefinition.Steps)
        {
            List<string> waitEvent = [];

            void register(FlowInput flowInput)
            {
                if (flowInput.Mode == InputMode.Event)
                {
                    var key = flowInput.Mode + flowInput.Value;
                    waitEvent.Add(key);
                    if (!ExecuteStepIds.TryGetValue(key, out var list))
                    {
                        list = [];
                        ExecuteStepIds.Add(key, list);
                    }
                    list.Add(item.Id);
                    foreach (var subInput in flowInput.Inputs)
                    {
                        register(subInput);
                    }
                }
            }
            foreach (var wait in item.Ifs)
            {
                var checker = FlowDefinition.Checkers.FirstOrDefault(c => c.Id == wait.Key) ?? item.Checkers.FirstOrDefault(c => c.Id == wait.Key);
                if (checker is null)
                {
                    continue;
                }
                register(checker);
            }
            foreach (var input in item.Inputs)
            {
                register(input);
            }
            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.PreStep => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.StartFlow => "",
                    _ => ""
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
    public Dictionary<string, List<Guid>> ExecuteStepIds { get; } = [];
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
    /// <summary>
    /// 日志
    /// </summary>
    public List<string> Logs { get; set; } = [];

    /// <summary>
    /// 附加属性
    /// </summary>
    public Dictionary<string, object> ExtraData { get; set; } = [];

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

public class FlowLog
{
    public Guid Id { get; set; }
    public required string Category { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// 当前下标
    /// </summary>
    public int CurrentIndex { get; set; }
    /// <summary>
    /// 执行错误下标
    /// </summary>
    public int ErrorIndex { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<NameValue> Inputs { get; set; } = [];
    public List<NameValue> Outputs { get; set; } = [];

    public Dictionary<string, StepLog> StepLogs { get; set; } = [];
    public List<EventLog> Events { get; set; } = [];

    public List<string> Middlewares { get; set; } = [];
}
public class EventLog
{
    public DateTime Time { get; set; }
    public required string EventName { get; set; }
    public string? EventData { get; set; }
}
public class StepLog
{
    public Guid[] FlowIds { get; set; } = [];
    public Guid StepId { get; set; }
    public required string StepName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<StepOnceStatus> StepOnceLogs { get; set; } = [];
}