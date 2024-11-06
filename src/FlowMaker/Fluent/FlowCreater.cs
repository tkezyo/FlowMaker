namespace FlowMaker.Fluent;

public interface IFlowCreator
{
    FlowDefinition FlowDefinition { get; }
    IGlobalDataCreator SetGlobalData(string displayName, string name, FlowDataType flowDataType);
    IStepCreator<T> SetNextStep<T>(string displayName) where T : IStep;

    FlowDefinition Build();
}

public interface IGlobalDataCreator : IFlowCreator
{
    IGlobalDataCreator IsInput();
    IGlobalDataCreator IsOutput();
    IGlobalDataCreator SetArray(int rank);
    IGlobalDataCreator AppendOption(string displayName, string name);
    IGlobalDataCreator SetOptionProviderName(string name);
    IGlobalDataCreator SetDefaultValue(string defaultValue);
}

public interface IStepCreator<T> : IFlowCreator
    where T : IStep
{
    FlowStep FlowStep { get; }

    IInputCreator<TInput, T> SetInput<TInput>(string displayName, TInput? input = default);
    IInputArrayCreator<TValue, TInput, T> SetArrayInput<TValue, TInput>(string displayName, params int[] dim);

    IStepCreator<T> SetPreStep(string displayName);
    IStepCreator<T> SetWaitEvent(string eventName);
    IInputCreator<int, T> SetRepeat(int? times = null);
    IInputCreator<int, T> SetRetry(int? times = null);
    IInputCreator<double, T> SetTimeout(double? timeout = null);
    IInputCreator<ErrorHandling, T> SetErrorHandling(ErrorHandling? errorHandling = null);
    IInputCreator<bool, T> SetFinally(bool? value = null);
}
public interface IInputCreator<T, TStep> : IStepCreator<TStep>
  where TStep : IStep
{
    IInputCreator<T, TStep> WithEvent(string eventName);
    IInputCreator<T, TStep> WithGlobal(string globeName);
    IInputCreator<T, TStep> WithValue(T value);
}
public interface IInputArrayCreator<TValue, T, TStep> : IStepCreator<TStep>
    where TStep : IStep
{
    IInputArrayCreator<TValue, T, TStep> GetArray(params int[] indexes);
    IInputArrayCreator<TValue, T, TStep> WithEvent(string eventName);
    IInputArrayCreator<TValue, T, TStep> WithGlobal(string globeName);
    IInputArrayCreator<TValue, T, TStep> WithValue(TValue value);
}
public class FlowCreator(FlowDefinition flowDefinition) : IFlowCreator
{
    public FlowCreator(string category, string name) : this(new FlowDefinition(category, name))
    {

    }
    public FlowDefinition FlowDefinition { get; } = flowDefinition;

    public FlowDefinition Build()
    {
        return FlowDefinition;
    }

    public IGlobalDataCreator SetGlobalData(string displayName, string name, FlowDataType flowDataType)
    {
        return new GlobalDataCreator(FlowDefinition, displayName, name, flowDataType);
    }

    public IStepCreator<T> SetNextStep<T>(string displayName) where T : IStep
    {
        return new StepCreator<T>(FlowDefinition, displayName);
    }
}
public class GlobalDataCreator : FlowCreator, IGlobalDataCreator
{
    public GlobalDataCreator(FlowDefinition flowDefinition, string displayName, string name, FlowDataType flowDataType) : base(flowDefinition)
    {
        DataDefinition = new DataDefinition(name, displayName, flowDataType);
        flowDefinition.Data.Add(DataDefinition);
    }

    private DataDefinition DataDefinition { get; }

    public IGlobalDataCreator AppendOption(string displayName, string name)
    {
        DataDefinition.Options.Add(new OptionDefinition(displayName, name));
        return this;
    }

    public IGlobalDataCreator IsInput()
    {
        DataDefinition.IsInput = true;
        return this;
    }

    public IGlobalDataCreator IsOutput()
    {
        DataDefinition.IsOutput = true;
        return this;
    }

    public IGlobalDataCreator SetArray(int rank)
    {
        DataDefinition.IsArray = true;
        DataDefinition.Rank = rank;
        return this;
    }

    public IGlobalDataCreator SetDefaultValue(string defaultValue)
    {
        DataDefinition.DefaultValue = defaultValue;
        return this;
    }



    public IGlobalDataCreator SetOptionProviderName(string name)
    {
        DataDefinition.OptionProviderName = name;
        return this;
    }


}

public class StepCreator<T>(FlowDefinition flowDefinition, FlowStep flowStep) : FlowCreator(flowDefinition), IStepCreator<T>
    where T : IStep
{
    public FlowStep FlowStep { get; } = flowStep;

    public StepCreator(FlowDefinition flowDefinition, string displayName) : this(flowDefinition, new FlowStep(displayName, T.Category, T.Name))
    {
        flowDefinition.Steps.Add(FlowStep);
    }
    public IStepCreator<T> SetPreStep(string displayName)
    {
        var preStep = FlowDefinition.Steps.FirstOrDefault(c => c.DisplayName == FlowStep.DisplayName);
        if (preStep is null)
        {
            throw new Exception("未找到步骤");
        }
        FlowStep.WaitEvents.Add(new FlowEvent { StepId = preStep.Id, Type = EventType.PreStep });
        return this;
    }

    public IInputCreator<double, T> SetTimeout(double? timeout)
    {
        var r = new InputCreator<double, T>(FlowDefinition, FlowStep, FlowStep.Timeout);
        if (timeout is not null)
        {
            r.WithValue(timeout.Value);
        }
        return r;
    }

    public IInputCreator<int, T> SetRetry(int? times = null)
    {
        var r = new InputCreator<int, T>(FlowDefinition, FlowStep, FlowStep.Retry);
        if (times is not null)
        {
            r.WithValue(times.Value);
        }
        return r;
    }

    public IInputCreator<int, T> SetRepeat(int? times = null)
    {
        var r = new InputCreator<int, T>(FlowDefinition, FlowStep, FlowStep.Repeat);
        if (times is not null)
        {
            r.WithValue(times.Value);
        }
        return r;
    }

    public IInputCreator<ErrorHandling, T> SetErrorHandling(ErrorHandling? errorHandling = null)
    {
        var r = new InputCreator<ErrorHandling, T>(FlowDefinition, FlowStep, FlowStep.ErrorHandling);

        if (errorHandling is not null)
        {
            r.WithValue(errorHandling.Value);
        }
        return r;
    }

    public IInputCreator<bool, T> SetFinally(bool? value = null)
    {
        var r = new InputCreator<bool, T>(FlowDefinition, FlowStep, FlowStep.Finally);
        if (value is not null)
        {
            r.WithValue(value.Value);
        }
        return r;
    }

    public IInputCreator<TInput, T> SetInput<TInput>(string displayName, TInput? input = default)
    {
        var r = new InputCreator<TInput, T>(FlowDefinition, FlowStep, displayName);

        if (input is not null)
        {
            r.WithValue(input);
        }
        return r;
    }

    public IInputArrayCreator<TValue, TInput, T> SetArrayInput<TValue, TInput>(string displayName, params int[] dim)
    {
        return new InputArrayCreator<TValue, TInput, T>(FlowDefinition, FlowStep, new FlowInput(displayName), dim);
    }

    public IStepCreator<T> SetWaitEvent(string eventName)
    {
        FlowStep.WaitEvents.Add(new FlowEvent { Type = EventType.Event, EventName = eventName });
        return this;
    }
}


public class InputCreator<T, TStep>(FlowDefinition flowDefinition, FlowStep flowStep, FlowInput flowInput) : StepCreator<TStep>(flowDefinition, flowStep), IInputCreator<T, TStep>
    where TStep : IStep
{
    public InputCreator(FlowDefinition flowDefinition, FlowStep flowStep, string displayName)
        : this(flowDefinition, flowStep, new FlowInput(displayName))
    {
        flowStep.Inputs.Add(FlowInput);
    }

    public FlowInput FlowInput { get; } = flowInput;
    public IInputCreator<T, TStep> WithValue(T? value)
    {
        FlowInput.Mode = InputMode.Normal;
        FlowInput.Value = value?.ToString();

        return this;
    }

    public IInputCreator<T, TStep> WithEvent(string eventName)
    {
        FlowInput.Mode = InputMode.Event;
        FlowInput.Value = eventName;

        return this;
    }

    public IInputCreator<T, TStep> WithGlobal(string globeName)
    {
        FlowInput.Mode = InputMode.Global;
        FlowInput.Value = globeName;

        return this;
    }





}

public class InputArrayCreator<TValue, T, TStep> : StepCreator<TStep>, IInputArrayCreator<TValue, T, TStep>
   where TStep : IStep
{
    public FlowInput FlowInput { get; }
    public FlowInput? SubInput { get; private set; }

    public InputArrayCreator(FlowDefinition flowDefinition, FlowStep flowStep, FlowInput flowInput, params int[] dim) : base(flowDefinition, flowStep)
    {
        FlowInput = flowInput;
        FlowInput.Dims = dim;
        WithArray(dim);

        flowStep.Inputs.Add(FlowInput);
    }
    protected IInputArrayCreator<TValue, T, TStep> WithArray(params int[] dim)
    {
        if (FlowInput.Dims.Length == 0)
        {
            throw new Exception("未设置数组维度");
        }
        FlowInput.Inputs.Clear();
        var count = dim.Aggregate((a, b) => a * b);

        for (int i = 0; i < count; i++)
        {
            int index = i;  // 一维索引

            int[] indices = new int[dim.Length];

            for (int j = dim.Length - 1; j >= 0; j--)
            {
                indices[j] = (index % dim[j]) + 1;
                index /= dim[j];
            }
            var indexName = string.Join(",", indices);
            FlowInput.Inputs.Add(new FlowInput(FlowInput.Name + $"({indexName})"));
        }

        return this;
    }

    public IInputArrayCreator<TValue, T, TStep> GetArray(params int[] indexes)
    {
        var indexName = string.Join(",", indexes);

        var subInput = FlowInput.Inputs.FirstOrDefault(c => c.Name == FlowInput.Name + $"({indexName})");
        if (subInput is null)
        {
            throw new Exception("未找到对应的数组元素");
        }
        SubInput = subInput;
        return this;
    }

    public IInputArrayCreator<TValue, T, TStep> WithEvent(string eventName)
    {
        FlowInput.Mode = InputMode.Event;
        FlowInput.Value = eventName;
        return this;
    }

    public IInputArrayCreator<TValue, T, TStep> WithGlobal(string globeName)
    {
        FlowInput.Mode = InputMode.Global;
        FlowInput.Value = globeName;
        return this;
    }

    public IInputArrayCreator<TValue, T, TStep> WithValue(TValue value)
    {
        if (SubInput is null)
        {
            throw new Exception("未找到对应的数组元素");
        }

        SubInput.Mode = InputMode.Normal;
        SubInput.Value = value?.ToString();
        return this;
    }
}

public class ConfigCreator
{
    public ConfigDefinition ConfigDefinition { get; set; }
    public ConfigCreator(string category, string name, string configName)
    {
        ConfigDefinition = new ConfigDefinition(category, name);
        ConfigDefinition.ConfigName = configName;
    }

    public ConfigCreator WithConfigName(string configName)
    {
        ConfigDefinition.ConfigName = configName;
        return this;
    }

    public ConfigCreator WithRetry(int retry)
    {
        ConfigDefinition.Retry = retry;
        return this;
    }

    public ConfigCreator WithRepeat(int repeat)
    {
        ConfigDefinition.Repeat = repeat;
        return this;
    }

    public ConfigCreator WithTimeout(int timeout)
    {
        ConfigDefinition.Timeout = timeout;
        return this;
    }

    public ConfigCreator WithLogView(string logView)
    {
        ConfigDefinition.LogView = logView;
        return this;
    }

    public ConfigCreator WithErrorStop(bool errorStop)
    {
        ConfigDefinition.ErrorStop = errorStop;
        return this;
    }

    public ConfigCreator WithData(string name, string value)
    {
        ConfigDefinition.Data.Add(new Ty.NameValue(name, value));
        return this;
    }

    public ConfigCreator WithFlowMiddlewares(params string[] names)
    {
        ConfigDefinition.FlowMiddlewares.Clear();
        ConfigDefinition.FlowMiddlewares.AddRange(names);
        return this;
    }

    public ConfigCreator WithStepGroupMiddlewares(params string[] names)
    {
        ConfigDefinition.StepGroupMiddlewares.Clear();
        ConfigDefinition.StepGroupMiddlewares.AddRange(names);
        return this;
    }

    public ConfigCreator WithStepMiddlewares(params string[] names)
    {
        ConfigDefinition.StepMiddlewares.Clear();
        ConfigDefinition.StepMiddlewares.AddRange(names);
        return this;
    }

    public ConfigDefinition Build()
    {
        return ConfigDefinition;
    }


}
