using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FlowMaker;

public interface IStep
{
    static abstract string Category { get; }
    static abstract string Name { get; }
    static abstract StepDefinition GetDefinition();
    protected Task Run(RunningContext context, FlowStep step, CancellationToken cancellationToken);
    Task WrapAsync(RunningContext context, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

public interface IFlowValueConverter
{
    static abstract string Category { get; }
    static abstract string Name { get; }
    static abstract ConverterDefinition GetDefinition();
    Task<string> GetStringResultAsync(RunningContext context, IDictionary<string, FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    public static async Task SetValue<TValue>(FlowOutput output, TValue value, IServiceProvider serviceProvider, RunningContext context, CancellationToken cancellationToken)
    {
        var valueStr = JsonSerializer.Serialize(value);
        if (!string.IsNullOrEmpty(output.ConverterCategory) && !string.IsNullOrEmpty(output.ConverterName) && !string.IsNullOrEmpty(output.InputKey))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(output.ConverterCategory, output.ConverterName);

            if (converterDefinition == null)
            {
                throw new InvalidOperationException();
            }
            var converterObj = serviceProvider.GetRequiredService(converterDefinition.Type);
            if (converterObj is IFlowValueConverter converter)
            {
                output.Inputs[output.InputKey] = new FlowInput()
                {
                    Value = valueStr,
                    UseGlobeData = false,
                    ConverterCategory = null,
                    ConverterName = null
                };
                var result = await converter.GetStringResultAsync(context, output.Inputs, serviceProvider, cancellationToken);
                context.Data[output.GlobeDataName].Value = result;

            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            context.Data[output.GlobeDataName].Value = valueStr;
        }
    }
}
public interface IFlowValueConverter<T> : IFlowValueConverter
{
    protected Task<T> Convert(RunningContext context, IDictionary<string, FlowInput> inputs, CancellationToken cancellationToken);
    Task<T> WrapAsync(RunningContext context, IDictionary<string, FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    public static async Task<T> GetValue(FlowInput input, IServiceProvider serviceProvider, RunningContext context, Func<string, T> convert, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(input.ConverterCategory) && !string.IsNullOrEmpty(input.ConverterName))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(input.ConverterCategory, input.ConverterName);

            if (converterDefinition == null)
            {
                throw new InvalidOperationException();
            }
            var converterObj = serviceProvider.GetRequiredService(converterDefinition.Type);
            if (converterObj is IFlowValueConverter<T> converter)
            {
                return await converter.WrapAsync(context, input.Inputs, serviceProvider, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            if (input.UseGlobeData && !string.IsNullOrEmpty(input.Value) && context.Data.TryGetValue(input.Value, out var data))
            {
                return convert.Invoke(data.Value ?? string.Empty);
            }
            else
            {
                return convert.Invoke(input.Value ?? string.Empty);
            }
        }
    }
}
