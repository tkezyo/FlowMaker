namespace FlowMaker.Models;

public class Step
{
    public Guid Id { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 输入
    /// </summary>
    public List<InputInfo> Inputs { get; set; } = new();
    /// <summary>
    /// 输出
    /// </summary>
    public List<OutputInfo> Outputs { get; set; } = new();
    /// <summary>
    /// 执行条件的处理方法，可以固定几个判断类型，< > == != 这种
    /// </summary>
    public string? ConditionFuncName { get; set; }

    /// <summary>
    /// 超时
    /// </summary>
    public TimeSpan TimeOut { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    public int Retry { get; set; }
    /// <summary>
    /// 重复
    /// </summary>
    public int Repeat { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    public ErrorHandlingType ErrorHandlingType { get; set; }
    /// <summary>
    /// 前置任务
    /// </summary>
    public List<Guid> PreActions { get; set; } = new();

}


public enum ErrorHandlingType
{
    Retry,
    Skip,
    Throw,
}

public class InputInfo
{
    public string DisplayName { get; set; }
    public string Name { get; set; }
    public string? Value { get; set; }

    public InputInfo(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}

public class OutputInfo
{
    public string DisplayName { get; set; }
    public string Name { get; set; }
    public string? Value { get; set; }

    public OutputInfo(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}