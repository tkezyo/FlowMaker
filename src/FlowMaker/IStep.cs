using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections;
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
    Task Run(FlowContext context, StepContext stepContext, CancellationToken cancellationToken);
    Task WrapAsync(FlowContext context, StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);
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
    public static async Task<T> GetValue<T>(FlowInput input, IServiceProvider serviceProvider, FlowContext? context, Func<string, T> convert, CancellationToken cancellationToken)
    {
        if (input.Mode == InputMode.Converter && !string.IsNullOrEmpty(input.ConverterCategory) && !string.IsNullOrEmpty(input.ConverterName))
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
            if (context is not null && input.Mode == InputMode.Globe && !string.IsNullOrEmpty(input.Value) && context.Data.TryGetValue(input.Value, out var data))
            {
                return convert.Invoke(data.Value ?? string.Empty);
            }
            else if (context is not null && input.Mode == InputMode.Event && !string.IsNullOrEmpty(input.Value) && context.EventData.TryGetValue(input.Value, out var eventData))
            {
                return convert.Invoke(eventData ?? string.Empty);
            }
            else
            {
                return convert.Invoke(input.Value ?? string.Empty);
            }
        }
    }

    public static async Task<List<T>> GetListValue<T>(FlowInput input, IServiceProvider serviceProvider, FlowContext? context, Func<string, T> convert, CancellationToken cancellationToken)
    {
        List<T> list = [];
        foreach (var item in input.Inputs)
        {
            list.Add(await GetValue<T>(item, serviceProvider, context, convert, cancellationToken));
        }
        return list;
    }
    public static async Task<T[]> GetArrayValue<T>(FlowInput input, IServiceProvider serviceProvider, FlowContext context, Func<string, T> convert, CancellationToken cancellationToken)
    {
        T[] values = new T[input.Inputs.Count];
        for (int i = 0; i < input.Inputs.Count; i++)
        {
            values[i] = await GetValue<T>(input.Inputs[i], serviceProvider, context, convert, cancellationToken);
        }

        return values;
    }



    public static Array Reshape<T>(int[] dims, T[] list)
    {
        return ReshapeRecursive(dims, list, 0);
    }

    private static Array ReshapeRecursive<T>(int[] dims, T[] list, int start)
    {
        if (dims.Length == 1)
        {
            T[] result = new T[dims[0]];
            Array.Copy(list, start, result, 0, dims[0]);
            return result;
        }
        else
        {
            Array[] result = new Array[dims[0]];
            int size = dims.Skip(1).Aggregate(1, (a, b) => a * b);
            for (int i = 0; i < dims[0]; i++)
            {
                result[i] = ReshapeRecursive(dims.Skip(1).ToArray(), list, start + i * size);
            }
            return result;
        }
    }
}
public interface IDataConverter<T> : IDataConverter
{
    Task<T> Convert(FlowContext? context, CancellationToken cancellationToken);
    Task<T> WrapAsync(FlowContext? context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);



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