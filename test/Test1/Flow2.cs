using FlowMaker;
using FlowMaker.Fluent;
using System.ComponentModel;
using Ty.Module.Configs;

namespace Test1;

public partial class Flow2 : IStep
{
    public static string Category => "Test1";

    public static string Name => "只有一个输入";

    [DefaultValue("0")]
    [Input]
    [Description("数字类型")]
    [Option("Min1", "0")]
    [Option("Max1", "100")]
    public int Integer { get; set; }


    [Input]
    public int[][]? Array { get; set; }


    public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        stepContext.Log(Integer.ToString());
        await Task.CompletedTask;
    }
}

public partial class Flow2
{
    public class Options
    {
        public class Integer
        {
            public const int Min1 = 0;
            public const int Max1 = 0;
        }
    }

}

public static class Flow2Extension
{
    public static IStepCreator<Flow2> NextFlow2(this IFlowCreator flow, string displayName)
    {
        return flow.SetNextStep<Flow2>(displayName);
    }

    public static IInputCreator<int, Flow2> SetInteger(this IStepCreator<Flow2> flow)
    {
        return flow.SetInput<int>(nameof(Flow2.Integer));
    }

    public static IInputArrayCreator<int, int[][], Flow2> SetArray(this IStepCreator<Flow2> flow, int dim1, int dim2)
    {
        return flow.SetArrayInput<int, int[][]>(nameof(Flow2.Array), dim1, dim2);
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