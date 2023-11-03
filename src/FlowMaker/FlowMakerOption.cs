using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker;

public class FlowMakerOption
{
    public Dictionary<string, FlowMakerOptionGroup> Group { get; set; } = new();
    public FlowMakerOptionGroup GetOrAddGroup(string category)
    {
        if (!Group.TryGetValue(category, out var group))
        {
            group = new FlowMakerOptionGroup();
            Group.Add(category, group);
        }
        return group;
    }
    public ConverterDefinition? GetConverter(string category, string name)
    {
        if (Group.TryGetValue(category, out var group))
        {
            return group.ConverterDefinitions.FirstOrDefault(c => c.Name == name);
        }
        return null;
    }
    public StepDefinition? GetStep(string category, string name)
    {
        if (Group.TryGetValue(category, out var group))
        {
            return group.StepDefinitions.FirstOrDefault(c => c.Name == name);
        }
        return null;
    }

}

public class FlowMakerOptionGroup
{
    public List<StepDefinition> StepDefinitions { get; set; } = new();
    public List<ConverterDefinition> ConverterDefinitions { get; set; } = new();
}

public static class FlowMakerExtention
{
    public static void AddFlowStep<T>(this IServiceCollection serviceDescriptors)
        where T : class, IStep
    {
        serviceDescriptors.AddTransient<T>();
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.Category);

            group.StepDefinitions.Add(T.GetDefinition());
        });
    }
    public static void AddFlowConverter<T>(this IServiceCollection serviceDescriptors)
        where T : class, IFlowValueConverter
    {
        serviceDescriptors.AddTransient<T>();
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.Category);

            group.ConverterDefinitions.Add(T.GetDefinition());
        });
    }

}