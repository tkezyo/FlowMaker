using FlowMaker;
using FlowMaker.Models;

[assembly: StepProviderName("123")]

namespace Test1;

public partial class Flow1 : IStep
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