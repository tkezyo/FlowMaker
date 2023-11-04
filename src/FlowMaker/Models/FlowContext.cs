using System.Collections.Concurrent;

namespace FlowMaker.Models;

public class FlowContext
{
    public FlowDefinition FlowDefinition { get; set; }

    public FlowContext(FlowDefinition flowDefinition)
    {
        FlowDefinition = flowDefinition;
    }

    /// <summary>
    /// 所有步骤的状态
    /// </summary>
    public Dictionary<Guid, StepResult> StepState { get; protected set; } = new();

    public List<Guid> SuspendSteps { get; protected set; } = new();
    public ConcurrentDictionary<string, FlowGlobeData> Data { get; set; } = new();
}

public class StepContext : FlowStep
{
    public int CurrentIndex { get; set; }

    public StepContext(List<FlowInput> inputs)
    {
        Inputs = inputs;
    }
}

public class FlowGlobeData
{
    public string Name { get; set; }

    public FlowGlobeData(string name, string type, string value)
    {
        Name = name;
        Type = type;
        Value = value;
    }

    public string Type { get; set; }
    public string Value { get; set; }
}
