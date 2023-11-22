using System.Collections.Concurrent;

namespace FlowMaker.Models;

public class FlowContext
{
    public FlowDefinition FlowDefinition { get; set; }

    public FlowContext(FlowDefinition flowDefinition)
    {
        FlowDefinition = flowDefinition;
        foreach (var item in FlowDefinition.Steps)
        {
            if (item.Compensate.HasValue)
            {
                continue;
            }
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
    public void InitState()
    {
        StepState.Clear();
        foreach (var item in FlowDefinition.Steps)
        {
            if (item.Compensate.HasValue)
            {
                continue;
            }
            var state = new StepResult();

            foreach (var wait in item.WaitEvents)
            {
                var key = wait.Type + wait.Type switch
                {
                    EventType.Step => wait.StepId?.ToString(),
                    EventType.Event => wait.EventName,
                    EventType.EventData => wait.EventName,
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
    public Dictionary<Guid, StepResult> StepState { get; protected set; } = [];
    public Dictionary<string, List<Guid>> ExcuteStepIds { get; } = [];

    public ConcurrentDictionary<string, FlowGlobeData> Data { get; set; } = new();

}

public class StepContext
{
    public int CurrentIndex { get; set; }
    public int ErrorIndex { get; set; }
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
}

public class FlowGlobeData(string name, string type, string value)
{
    public string Name { get; set; } = name;

    public string Type { get; set; } = type;
    public string Value { get; set; } = value;
}
public class StepResult
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
    public List<bool> Results { get; set; } = [];

    /// <summary>
    /// 消耗的时间
    /// </summary>
    public TimeSpan? ConsumeTime { get; set; }
    public List<string> Waits { get; set; } = [];
}

public class NameValue(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
}