using System.Collections.Concurrent;

namespace FlowMaker.Models;

public class RunningContext
{
    public FlowDefinition FlowDefinition { get; set; }

    public RunningContext(FlowDefinition flowDefinition)
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
