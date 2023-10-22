using FlowMaker;
using FlowMaker.Models;

[assembly: StepProviderName("123")]

namespace Test1;

public class Flow1 : IStep
{
    [Input("")]
    public int Prop1 { get; set; }
    [Input("")]
    public int Prop2 { get; set; }
    [Output("")]
    public int Prop3 { get; set; }

    [Input("")]
    public Data1? Data { get; set; }
    /// <summary>
    /// 这个要自动生成
    /// </summary>
    /// <param name="keyValues"></param>
    /// <returns></returns>
    public async Task WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken)
    {
        var keyProp1 = step.Inputs["Prop1"].Value;
        if (step.Inputs["Prop1"].UseGlobeData)
        {
            keyProp1 = context.Data[step.Inputs["Prop1"].Value];
        }
        Prop1 = Convert.ToInt32(keyProp1);

        Prop2 = Convert.ToInt32(context.Data[""]);
        Data = System.Text.Json.JsonSerializer.Deserialize<Data1>(context.Data[""]);
        await Run(context, step, cancellationToken);

        context.Data[step.Outputs["Prop1"]] = Prop3.ToString();
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