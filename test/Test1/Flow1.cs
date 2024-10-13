using FlowMaker;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Ty.Module.Configs;

namespace Test1;

public partial class MyClass : IStep
{
    public static string Category => "类别";

    public static string Name => "名称";

    public Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public partial class Flow1 : IStep
{
    public static string Category => "Test1";

    public static string Name => "Flow1";

    [Input]
    [OptionProvider(PortProvider.FullName)]
    public int Prop1 { get; set; }

    [Input]
    [DefaultValue("3")]
    [Description("属性2")]
    [Option("三", "3")]
    [Option("四", "4")]
    public int Prop2 { get; set; }

    [Output]
    public int Prop3 { get; set; }

    //[Input]
    // [Output]
    public Data1? Data { get; set; }



    /// <summary>
    /// 执行的命令
    /// </summary>
    /// <returns></returns>
    public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        Prop3 = 100;
        stepContext.Log("测试日志", LogLevel.Information);
        await Task.CompletedTask;
    }

}


public class Data1
{

}
