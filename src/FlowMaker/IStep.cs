using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowMaker;

public interface IStep
{
    static abstract string GroupName { get; }
    static abstract string Name { get; }
    static abstract StepDefinition GetDefinition();
    protected Task Run(RunningContext context, FlowStep step, CancellationToken cancellationToken);
    Task WrapAsync(RunningContext context, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}


public interface IFlowValueConverter<T>
{
    static abstract string GroupName { get; }
    static abstract string Name { get; }
    static abstract ConverterDefinition GetDefinition();
    protected Task<T> Convert(RunningContext context, FlowInput inputValues, CancellationToken cancellationToken);
    Task<T> WrapAsync(RunningContext context, FlowInput inputValues, CancellationToken cancellationToken);

    public static async Task<T> GetValue(string propName, IServiceProvider serviceProvider, RunningContext context, Dictionary<string, FlowInput> inputs, Func<string, T> convert, CancellationToken cancellationToken)
    {
        var input = inputs[propName];
        if (!string.IsNullOrEmpty(input.GroupName) && !string.IsNullOrEmpty(input.Name))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(input.GroupName, input.Name);

            if (converterDefinition != null)
            {
                var converterObj = serviceProvider.GetRequiredService(converterDefinition.Type);
                if (converterObj is IFlowValueConverter<T> converter)
                {
                    return await converter.WrapAsync(context, input, cancellationToken);
                }
            }
        }

        return GetValue(propName, context, inputs, convert);
    }
    public static T GetValue(string propName, RunningContext context, Dictionary<string, FlowInput> inputs, Func<string, T> convert)
    {
        var input = inputs[propName];

        var value = input.Values[0].Value;
        if (input.Values[0].UseGlobeData)
        {
            value = context.Data[value];
        }
        return convert(value);
    }
    public static T GetValue(string propName, RunningContext context, FlowInput input, Func<string, T> convert)
    {
        var inputa = input.Values.FirstOrDefault(c => c.PropName == propName);

        var value = inputa.Value;
        if (input.Values[0].UseGlobeData)
        {
            value = context.Data[value];
        }
        return convert(value);
    }
}
