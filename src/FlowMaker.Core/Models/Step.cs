namespace FlowMaker.Models;

public class Step
{
    /// <summary>
    /// 步骤唯一Id
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 执行类
    /// </summary>
    public required string RunnerName { get; set; }
    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 输入
    /// </summary>
    public Dictionary<string, Input> Inputs { get; set; } = new();
    /// <summary>
    /// 输出
    /// </summary>
    public Dictionary<string, string> Outputs { get; set; } = new();

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
    public ErrorHandling ErrorHandling { get; set; }
    /// <summary>
    /// 前置任务
    /// </summary>
    public List<Guid> PreActions { get; set; } = new();
    /// <summary>
    /// 回退任务
    /// </summary>
    public Guid? Compensate { get; set; }
    /// <summary>
    /// 是否可执行
    /// </summary>
    public Dictionary<Guid, bool> CanExcute { get; set; } = new();

    //TODO 需要判断根据全局输入的参数是否满足执行条件
    public Dictionary<string, bool> CanExcuteFromInput { get; set; } = new();

}

public class Input
{
    public string Value { get; set; }
    /// <summary>
    /// 使用全局变量
    /// </summary>
    public bool UseGlobeData { get; set; }
    public Input(string value)
    {
        Value = value;
    }
}

public enum ErrorHandling
{
    /// <summary>
    /// 跳过
    /// </summary>
    Skip,
    /// <summary>
    /// 暂停
    /// </summary>
    Suspend,
    /// <summary>
    /// 停止
    /// </summary>
    Terminate
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
    /// <summary>
    /// 完成次数
    /// </summary>
    public int CompleteTimes { get; set; }
    /// <summary>
    /// 错误次数
    /// </summary>
    public int ErrorTimes { get; set; }
    /// <summary>
    /// 消耗的时间
    /// </summary>
    public TimeSpan? ConsumeTime { get; set; }
}