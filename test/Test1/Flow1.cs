using FlowMaker;
using FlowMaker.Models;

namespace Test1;

[FlowStep(nameof(Test1), "流程1")]
public partial class Flow1
{
    [Input("Prop1")]
    public int Prop1 { get; set; }
    [DefaultValue("1")]
    [Input("Prop2")]
    [Option("123", "3")]
    [Option("234", "34")]
    public int Prop2 { get; set; }

    [Output("333")]
    public int Prop3 { get; set; }

    [Input("123")]
    public Data1? Data { get; set; }

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
    [Input("双方1")]
    public int Prop1 { get; set; }
    [Input("问1")]
    public int Prop2 { get; set; }

    public async Task<int> Convert(RunningContext context, IDictionary<string, FlowInput> inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Prop1 + Prop2;
    }


}