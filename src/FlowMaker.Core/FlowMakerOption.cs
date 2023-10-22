using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker;

public class FlowMakerOption
{
    public Dictionary<string, FlowMakerOptionGroup> Group { get; set; } = new();
    public FlowMakerOptionGroup GetOrAddGroup(string groupName)
    {
        if (!Group.TryGetValue(groupName, out var group))
        {
            group = new FlowMakerOptionGroup();
            Group.Add(groupName, group);
        }
        return group;
    }
}

public class FlowMakerOptionGroup
{
    public List<StepDefinition> StepDefinitions { get; set; } = new();
    public List<StepDefinition> CheckStepDefinitions { get; set; } = new();
    public List<ConvertorDefinition> ConvertorDefinitions { get; set; } = new();
}

public static class FlowMakerExtention
{
    public static void AddFlowStep<T>(this IServiceCollection serviceDescriptors)
        where T : class, IStep
    {
        serviceDescriptors.AddTransient<T>();
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.GroupName);
            if (typeof(T) is IExcuteStep)
            {
                group.StepDefinitions.Add(T.GetDefinition());
            }
            if (typeof(T) is ICheckStep)
            {
                group.CheckStepDefinitions.Add(T.GetDefinition());
            }
        });
    }
    public static void AddFlowStep<T>(this IServiceCollection serviceDescriptors)
        where T : class, IStep
    {
        serviceDescriptors.AddTransient<T>();
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.GroupName);
            if (typeof(T) is IExcuteStep)
            {
                group.StepDefinitions.Add(T.GetDefinition());
            }
            if (typeof(T) is ICheckStep)
            {
                group.CheckStepDefinitions.Add(T.GetDefinition());
            }
        });
    }

}