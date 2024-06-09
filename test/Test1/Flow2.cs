using FlowMaker;
using System.ComponentModel;
using Ty.Module.Configs;

namespace Test1;

public partial class Flow2 : IStep
{
    public static string Category => "Test1";

    public static string Name => "只有一个输入";


    [Input]
    [Description("数字类型")]
    public int Integer { get; set; }


    public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        await stepContext.Log(Integer.ToString());
    }
}

public partial class Flow3 : IStep
{
    public static string Category => "Test1";

    public static string Name => "输入输出各一个";

    [Input]
    public int Input { get; set; }
    [Output]
    public int Output { get; set; }
    public Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        Output = Input * 2;
        return Task.CompletedTask;
    }
}