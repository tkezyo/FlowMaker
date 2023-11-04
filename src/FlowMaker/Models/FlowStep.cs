namespace FlowMaker.Models;

public class FlowStep
{
    /// <summary>
    /// 步骤唯一Id
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// 显示名称
    /// </summary>
    public required string DisplayName { get; set; }
    /// <summary>
    /// 步骤的类别
    /// </summary>
    public required string Category { get; set; }
    /// <summary>
    /// 步骤的名称
    /// </summary>
    public required string Name { get; set; }
   
    public bool IsCompensateStep { get; set; }
    /// <summary>
    /// 输入
    /// </summary>
    public List<FlowInput> Inputs { get; set; } = new();
    /// <summary>
    /// 输出
    /// </summary>
    public List<FlowOutput> Outputs { get; set; } = new();

    /// <summary>
    /// 超时,秒
    /// </summary>
    public double TimeOut { get; set; }

    /// <summary>
    /// 重试
    /// </summary>
    public int Retry { get; set; }
    /// <summary>
    /// 重复,如果是负数，则一直重复
    /// </summary>
    public int Repeat { get; set; }
    /// <summary>
    /// 出现错误时处理方式
    /// </summary>
    public ErrorHandling ErrorHandling { get; set; }
    /// <summary>
    /// 前置任务
    /// </summary>
    public List<Guid> PreSteps { get; set; } = new();

    /// <summary>
    /// 回退任务,stepdId
    /// </summary>
    public Guid? Compensate { get; set; }



    /// <summary>
    /// 是否可执行，同时可作为Break的条件
    /// </summary>
    public Dictionary<Guid, bool> If { get; set; } = new();
    public List<FlowInput> Checkers { get; set; } = new();
}


public class FlowInput
{
    public required string Name { get; set; }
    public required Guid Id { get; set; }
    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }

    public bool UseGlobeData { get; set; }
    public string? Value { get; set; }
    public List<FlowInput> Inputs { get; protected set; } = new();
}

public class FlowOutput
{
    public required string Name { get; set; }

    public string? ConverterCategory { get; set; }
    public string? ConverterName { get; set; }
    public string? InputKey { get; set; }


    public string? ConvertToType { get; set; }
    public required string Type { get; set; }

    public required string GlobeDataName { get; set; }
    public List<FlowInput> Inputs { get; protected set; } = new();
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