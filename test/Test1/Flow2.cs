using FlowMaker;
using FlowMaker.Models;
using System.ComponentModel;

namespace Test1;

public partial class Flow2 : IStep
{
    public static string Category => "Test1";

    public static string Name => "只有一个输入";

    [Input]
    public string[]? MyProperty { get; set; }

    [Input]
    [Description("数字类型")]
    public int Integer { get; set; }


    public Task Run(FlowContext context, StepContext stepContext, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}