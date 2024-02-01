using FlowMaker;
using FlowMaker.Models;
using System.ComponentModel;
using System.Text.Json;

namespace Test1;
public partial class MyClass : IStep
{
    public static string Category => "类别";

    public static string Name => "名称";

    public Task Run(FlowContext context, StepContext stepContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public partial class Flow1 : IStep
{
    public static string Category => "Test1";

    public static string Name => "Flow1";

    [Input]
    [OptionProvider<PortProvider>]
    public int Prop1 { get; set; }

    [Input]
    [DefaultValue("3")]
    [Description("属性2")]
    [Option("三", "3")]
    [Option("四", "4")]
    public int Prop2 { get; set; }

    [Output]
    public int Prop3 { get; set; }

    [Input]
    public Data1? Data { get; set; }

    /// <summary>
    /// 执行的命令
    /// </summary>
    /// <returns></returns>
    public Task Run(FlowContext context, StepContext stepContext, CancellationToken cancellationToken)
    {
        Prop3 = 100;
        return Task.CompletedTask;
    }

}


public class Data1
{

}

public partial class ValueConverter : IDataConverter<int>
{
    public static string Category => "Test1";

    public static string Name => "转换器1";

    [Input]
    public int Prop1 { get; set; }
    [Input]
    public int Prop2 { get; set; }

    public Task<int> Convert(FlowContext? context, CancellationToken cancellationToken)
    {
        return Task.FromResult(Prop1 + Prop2);
    }


}