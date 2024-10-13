using DynamicData;
using System.Runtime.CompilerServices;
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
    Task Run(StepContext stepContext, CancellationToken cancellationToken);
    Task WrapAsync(StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    public static void SetValue<TValue>(FlowOutput output, TValue value, FlowContext context)
    {
        if (output.Mode == OutputMode.Drop || string.IsNullOrEmpty(output.GlobalDataName))
        {
            return;
        }
        var valueStr = JsonSerializer.Serialize(value).Trim('"');

        var data = context.Data.Lookup(output.GlobalDataName);
        if (!data.HasValue)
        {
            context.Data.AddOrUpdate(new FlowGlobeData(output.GlobalDataName, output.Type, valueStr));
        }
        else
        {
            data.Value.Value = valueStr;
            context.Data.AddOrUpdate(data.Value);
        }
    }
    public static string GetValue(FlowInput input, FlowContext context, string? defaultValue)
    {
        if (input.Mode == InputMode.Global && !string.IsNullOrEmpty(input.Value))
        {
            var data = context.Data.Lookup(input.Value);
            if (data.HasValue)
            {
                return data.Value.Value ?? string.Empty;
            }
            else
            {
                return defaultValue ?? string.Empty;
            }
        }
        else
        {
            return input.Value ?? defaultValue ?? string.Empty;
        }
    }
    public static T GetValue<T>(FlowInput input, FlowContext? context, Func<string, T> convert, string? name = null)
    {
        try
        {
            if (context is not null && input.Mode == InputMode.Global && !string.IsNullOrEmpty(input.Value))
            {
                var data = context.Data.Lookup(input.Value);
                if (!data.HasValue)
                {
                    return convert.Invoke(string.Empty);
                }
                else
                {
                    return convert.Invoke(data.Value.Value ?? string.Empty);
                }
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
        catch (Exception e)
        {
            throw new Exception($"{name} value error", e);
        }

    }

    public static List<T> GetListValue<T>(FlowInput input, FlowContext? context, Func<string, T> convert)
    {
        List<T> list = [];
        foreach (var item in input.Inputs)
        {
            list.Add(GetValue(item, context, convert));
        }
        return list;
    }
    public static T[] GetArrayValue<T>(FlowInput input, FlowContext context, Func<string, T> convert)
    {
        T[] values = new T[input.Inputs.Count];
        for (int i = 0; i < input.Inputs.Count; i++)
        {
            values[i] = GetValue(input.Inputs[i], context, convert);
        }

        return values;
    }

    public static Array Reshape<T>(int[] dims, T[] list)
    {
        var array = ReshapeRecursive(dims, list, 0);
        return array;
    }

    /// <summary>
    /// 递归重塑数组, 用于多维数组
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dims">数组的维度</param>
    /// <param name="list">要重塑的数组</param>
    /// <param name="start">起始索引</param>
    /// <returns>重塑后的数组</returns>
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

    public static T ConvertToArray<T>(Array array)
    {
        var r = (T)ConvertToArrayRecursive(array, typeof(T));
        return r;
    }

    private static object ConvertToArrayRecursive(Array array, Type targetType)
    {
        if (targetType.IsArray)
        {
            Type elementType = targetType.GetElementType()!;
            int length = array.GetLength(0);
            Array resultArray = Array.CreateInstance(elementType, length);

            for (int i = 0; i < length; i++)
            {
                var value = array.GetValue(i);
                if (value is Array subArray)
                {
                    resultArray.SetValue(ConvertToArrayRecursive(subArray, elementType), i);
                }
                else if (value is not null && elementType.IsAssignableFrom(value.GetType()))
                {
                    resultArray.SetValue(value, i);
                }
                else
                {
                    throw new InvalidCastException($"Element at index {i} is not of type {elementType}.");
                }
            }

            return resultArray;
        }
        else
        {
            return array;
        }
    }
}


[System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class StepsAttribute : Attribute
{
    readonly string category;

    public StepsAttribute(string category)
    {
        this.category = category;
    }

    public string Category
    {
        get { return category; }
    }
}