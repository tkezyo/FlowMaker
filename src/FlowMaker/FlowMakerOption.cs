using Microsoft.Extensions.DependencyInjection;
using Ty;

namespace FlowMaker;

public class FlowMakerOption
{
    public string DebugPageRootDir { get; set; } = string.Empty;
    public string FlowRootDir { get; set; } = string.Empty;
    /// <summary>
    /// 程序启动时是否自动运行
    /// </summary>
    public bool AutoRun { get; set; } = false;
    /// <summary>
    /// 是否可调试
    /// </summary>
    public bool CanDebug { get; set; } = true;
    /// <summary>
    /// 是否可编辑
    /// </summary>
    public bool Edit { get; set; } = true;
    /// <summary>
    /// 自定义页面
    /// </summary>
    public string? Section { get; set; }

    public int MaxColCount { get; set; } = 4;


    /// <summary>
    /// 包含所有的步骤及转换器
    /// </summary>
    public Dictionary<string, FlowMakerOptionGroup> Group { get; set; } = [];
    /// <summary>
    /// 选项集
    /// </summary>
    public Dictionary<string, List<NameValue>> OptionProviders { get; set; } = [];
    /// <summary>
    /// 可选的中间件
    /// </summary>
    public List<NameValue> Middlewares { get; set; } = [];
    /// <summary>
    /// 默认插入的中间件
    /// </summary>
    public List<NameValue> DefaultMiddlewares { get; set; } = [];

    /// <summary>
    /// 自定义日志视图
    /// </summary>
    public List<string> CustomLogViews { get; set; } = [];

    public FlowMakerOptionGroup GetOrAddGroup(string category)
    {
        if (!Group.TryGetValue(category, out var group))
        {
            group = new FlowMakerOptionGroup();
            Group.Add(category, group);
        }
        return group;
    }
    public List<NameValue> GetOrAddType(string type)
    {
        if (!OptionProviders.TryGetValue(type, out var group))
        {
            group = [];
            OptionProviders.Add(type, group);
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

public static partial class FlowMakerExtension
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