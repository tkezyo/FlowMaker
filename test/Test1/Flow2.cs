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
            public const string Min1 = "0";
            public const string Max1 = "0";
        }
    }

}

public static class Flow2Extension
{
    public static IStepCreater<Flow2> NextFlow2(this IFlowCreater flow, string displayName)
    {
        return new StepCreater<Flow2>(flow.FlowDefinition, displayName);
    }

    public static IInputCreater<int, Flow2> SetInteger(this IStepCreater<Flow2> flow)
    {
        return new InputCreater<int, Flow2>(flow.FlowDefinition, flow.FlowStep, nameof(Flow2.Integer));
    }

    public static IInputCreater<int[][], Flow2> SetArray(this IStepCreater<Flow2> flow, int dim1, int dim2)
    {
        return new InputCreater<int[][], Flow2>(flow.FlowDefinition, flow.FlowStep, nameof(Flow2.Integer)).WithArray(dim1, dim2);
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