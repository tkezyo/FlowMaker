using FlowMaker;
using FlowMaker.Models;

namespace Test1;

[FlowStep("test1", "流程1")]
public partial class Flow1
{
    [Input("")]
    public int Prop1 { get; set; }
    [Input("")]
    [Option("123","3")]
    public int Prop2 { get; set; }
    [DefaultValue("1")]
    [Output("333")]
    public int Prop3 { get; set; }

    [Input("123")]
    public Data1? Data { get; set; }

    public static StepDefinition GetDefinition2()
    {
        return new StepDefinition
        {
            DisplayGroup = "",
            DisplayName = "流程1",
            Name = "Test1.Flow1",
            Type = typeof(Flow1),
            Inputs = new List<StepInputDefinition>
                {
                    new StepInputDefinition("","",""),
                    new StepInputDefinition("","",""),
                },
            Outputs = new List<StepOutputDefinition>
                {
                    new StepOutputDefinition("","",""),
                }
        };
    }


    /// <summary>
    /// 执行的命令
    /// </summary>
    /// <returns></returns>
    public Task Run(RunningContext context, FlowStep step, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


public class Data1
{

}

[FlowConverter<int>(nameof(Test1), "流程1")]
public partial class ValueConverter
{
    [Input("")]
    public int Prop1 { get; set; }
    [Input("")]
    public int Prop2 { get; set; }

    public async Task<int> Convert(RunningContext context, FlowInput step, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Prop1 + Prop2;
    }

   
}