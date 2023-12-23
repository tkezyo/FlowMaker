using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FlowMaker;

public interface IStep : IStepInject
{
    /// <summary>
    /// 类别
    /// </summary>
    static abstract string Category { get; }
    /// <summary>
    /// 名称
    /// </summary>
    static abstract string Name { get; }
    static abstract StepDefinition GetDefinition();
}

public interface IStepInject
{
    Task Run(FlowContext context, StepContext stepContext, FlowStep step, CancellationToken cancellationToken);
    Task WrapAsync(FlowContext context, StepContext stepContext, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}


public interface IDataConverter : IDataConverterInject
{
    static abstract string Category { get; }
    static abstract string Name { get; }
    static abstract ConverterDefinition GetDefinition();
}
public interface IDataConverterInject
{
    Task<string> GetStringResultAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    public static async Task SetValue<TValue>(FlowOutput output, TValue value, IServiceProvider serviceProvider, FlowContext context, CancellationToken cancellationToken)
    {
        if (output.Mode == OutputMode.Drop || string.IsNullOrEmpty(output.GlobeDataName))
        {
            return;
        }
        var valueStr = JsonSerializer.Serialize(value);
        if (!string.IsNullOrEmpty(output.ConverterCategory) && !string.IsNullOrEmpty(output.ConverterName) && !string.IsNullOrEmpty(output.InputKey))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(output.ConverterCategory, output.ConverterName);

            if (converterDefinition == null)
            {
                throw new InvalidOperationException();
            }
            var converterObj = serviceProvider.GetRequiredKeyedService<IDataConverterInject>(converterDefinition.Category + ":" + converterDefinition.Name);
            if (converterObj is IDataConverter converter)
            {
                output.Inputs.RemoveAll(x => x.Name == output.InputKey);
                output.Inputs.Add(new FlowInput(output.InputKey)
                {
                    Value = valueStr,
                    Mode = InputMode.Normal,
                    ConverterCategory = null,
                    ConverterName = null
                });

                var result = await converter.GetStringResultAsync(context, output.Inputs, serviceProvider, cancellationToken);
                if (!context.Data.TryGetValue(output.GlobeDataName, out var data))
                {
                    context.Data.TryAdd(output.GlobeDataName, new FlowGlobeData(output.Name, output.Type, result));
                }
                else
                {
                    data.Value = result;
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            if (!context.Data.TryGetValue(output.GlobeDataName, out var data))
            {
                context.Data.TryAdd(output.GlobeDataName, new FlowGlobeData(output.Name, output.Type, valueStr));
            }
            else
            {
                data.Value = valueStr;
            }
        }
    }
    public static async Task<string> GetValue(FlowInput input, IServiceProvider serviceProvider, FlowContext context, string? defaultValue, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(input.ConverterCategory) && !string.IsNullOrEmpty(input.ConverterName))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(input.ConverterCategory, input.ConverterName) ?? throw new InvalidOperationException();

            var converterObj = serviceProvider.GetRequiredKeyedService<IDataConverterInject>(converterDefinition.Category + ":" + converterDefinition.Name);
            return await converterObj.GetStringResultAsync(context, input.Inputs, serviceProvider, cancellationToken);
        }
        else
        {
            if (input.Mode == InputMode.Globe && !string.IsNullOrEmpty(input.Value) && context.Data.TryGetValue(input.Value, out var data))
            {
                return data.Value ?? defaultValue ?? string.Empty;
            }
            else
            {
                return input.Value ?? defaultValue ?? string.Empty;
            }
        }
    }
}
public interface IDataConverter<T> : IDataConverter
{
    Task<T> Convert(FlowContext context, CancellationToken cancellationToken);
    Task<T> WrapAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);


    public static async Task<T> GetValue(FlowInput input, IServiceProvider serviceProvider, FlowContext context, Func<string, T> convert, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(input.ConverterCategory) && !string.IsNullOrEmpty(input.ConverterName))
        {
            var option = serviceProvider.GetRequiredService<IOptions<FlowMakerOption>>();
            var converterDefinition = option.Value.GetConverter(input.ConverterCategory, input.ConverterName);

            if (converterDefinition == null)
            {
                throw new InvalidOperationException();
            }
            var converterObj = serviceProvider.GetKeyedService<IDataConverterInject>(converterDefinition.Category + ":" + converterDefinition.Name);
            if (converterObj is IDataConverter<T> converter)
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
            if (input.Mode == InputMode.Globe && !string.IsNullOrEmpty(input.Value) && context.Data.TryGetValue(input.Value, out var data))
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

public interface IOptionProvider<T> : IOptionProvider
{
}
public interface IOptionProvider : IOptionProviderInject
{
    /// <summary>
    /// 名称
    /// </summary>
    static abstract string DisplayName { get; }
    static abstract string Name { get; }
    static abstract string Type { get; }
}
public interface IOptionProviderInject
{
    Task<IEnumerable<NameValue>> GetOptions();
}

public interface IFlowMiddleware
{
    Task OnExecuting(FlowContext flowContext, RunnerState runnerState, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, RunnerState runnerState, CancellationToken cancellationToken);
    Task OnError(FlowContext flowContext, RunnerState runnerState, Exception exception, CancellationToken cancellationToken);
}
public interface IStepMiddleware
{
    Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken);
    Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, Exception exception, CancellationToken cancellationToken);
}
public interface IStepOnceMiddleware
{
    Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken);
    Task OnError(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception exception, CancellationToken cancellationToken);
}