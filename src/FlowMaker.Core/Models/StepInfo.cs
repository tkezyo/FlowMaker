using Volo.Abp;

namespace FlowMaker.Models;

public class FlowDefinition
{
    public List<Step> Steps { get; set; } = new();
    public List<StepInput> Inputs { get; set; } = new();
    public List<StepOutput> Outputs { get; set; } = new();
    /// <summary>
    /// 批量设置输入,将所有步骤的输入设置为相同的值
    /// </summary>
    public Dictionary<string, string> SetInputs { get; set; } = new();
}
public class StepInfo
{
    /// <summary>
    /// 执行类
    /// </summary>
    public required string ExcutorName { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    public List<StepInput> Inputs { get; set; } = new();
    public List<StepOutput> Outputs { get; set; } = new();
}
public class StepOutput
{
    public required string Name { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public required string Description { get; set; }
}

public class StepInput
{
    public ParameterType Type { get; set; }

    public string Name { get; set; }
    /// <summary>
    /// 显示名称，描述
    /// </summary>
    public string Description { get; set; }

    public string? DefaultValue { get; set; }


    public List<NameValue> Options { get; set; }

    public InputDisplayType DisplayType { get; set; }

    public StepInput(string name, string description, ParameterType type)
    {
        Type = type;
        Name = name;
        Options = new List<NameValue>();
        DisplayType = InputDisplayType.Text;
        Description = description;
    }
}

public enum InputDisplayType
{
    Text,
    ComboBox,
    CheckBox,
}

public enum ParameterType
{
    String,
    Number,
    Enum,
    Boolean,
    Array,
}