using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace FlowMaker;

public class FlowMakerOption
{
    public string RootDir { get; set; } = string.Empty;
    public bool Edit { get; set; } = true;
    public List<string> Sections { get; set; } = [];
    public List<string> CustomViews { get; set; } = [];
    public Dictionary<string, FlowMakerOptionGroup> Group { get; set; } = [];
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
    public List<StepDefinition> StepDefinitions { get; set; } = [];
    public List<ConverterDefinition> ConverterDefinitions { get; set; } = [];
}

public static class FlowMakerExtension
{

    public static void AddFlowStep<T>(this IServiceCollection serviceDescriptors)
        where T : class, IStep
    {
        serviceDescriptors.AddKeyedTransient<IStepInject, T>(T.Category + ":" + T.Name);
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.Category);

            group.StepDefinitions.Add(T.GetDefinition());
        });
    }
    public static void AddFlowConverter<T>(this IServiceCollection serviceDescriptors)
        where T : class, IDataConverter
    {
        serviceDescriptors.AddKeyedTransient<IDataConverterInject, T>(T.Category + ":" + T.Name);
        serviceDescriptors.Configure<FlowMakerOption>(c =>
        {
            var group = c.GetOrAddGroup(T.Category);

            group.ConverterDefinitions.Add(T.GetDefinition());
        });
    }

}