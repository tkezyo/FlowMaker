using FlowMaker;
using FlowMaker.Models;

namespace Test1;

[FlowStep(nameof(Test1), "流程1", false)]
public partial class Flow1
{
    [Input("")]
    public int Prop1 { get; set; }
    [Input("")]
    public int Prop2 { get; set; }
    [DefaultValue("1")]
    [Output("")]
    public int Prop3 { get; set; }

    [Input("")]
    public Data1? Data { get; set; }

    public static StepDefinition GetDefinition()
    {
        return new StepDefinition
        {
            DiaplayName = "流程1",
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
    public Task Run(RunningContext context, Step step, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


public class Data1
{

}

public class ValueConverter : IStepValueConverter<string, int>
{
    public string Name => "123";

    public int ConvertTo(string from)
    {
        return Convert.ToInt32(from);
    }
}