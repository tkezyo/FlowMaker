using DynamicData.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker.Fluent
{
    public interface IFlowCreater
    {
        FlowDefinition FlowDefinition { get; }

        FlowDefinition Build();
    }

    public interface IStepCreater<T> : IFlowCreater
        where T : IStep
    {
        FlowStep FlowStep { get; }

        IInputCreater<ErrorHandling, T> SetErrorHandling();
        IInputCreater<bool, T> SetFinally();
        IStepCreater<T> SetPreStep(string displayName);
        IInputCreater<int, T> SetRepeat();
        IInputCreater<int, T> SetRetry();
        IInputCreater<double, T> SetTimeout();
    }
    public interface IInputCreater<T, TStep> : IStepCreater<TStep>
      where TStep : IStep
    {
        IInputArrayCreater<T, TStep> GetArray(params int[] indexes);
        IInputCreater<T, TStep> WithArray(params int[] dim);
        IInputCreater<T, TStep> WithEvent(string eventName);
        IInputCreater<T, TStep> WithGlobal(string globeName);
        IInputCreater<T, TStep> WithValue(string value);
    }
    public interface IInputArrayCreater<T, TStep> : IInputCreater<T, TStep>
        where TStep : IStep
    {

    }
    public class FlowCreater(FlowDefinition flowDefinition) : IFlowCreater
    {
        public FlowCreater(string category, string name) : this(new FlowDefinition(category, name))
        {

        }
        public FlowDefinition FlowDefinition { get; } = flowDefinition;

        public FlowDefinition Build()
        {
            return FlowDefinition;
        }
    }
    public class StepCreater<T>(FlowDefinition flowDefinition, FlowStep flowStep) : FlowCreater(flowDefinition), IStepCreater<T>
        where T : IStep
    {
        public FlowStep FlowStep { get; } = flowStep;

        public StepCreater(FlowDefinition flowDefinition, string dispayName) : this(flowDefinition, new FlowStep(dispayName, T.Category, T.Name))
        {

        }
        public IStepCreater<T> SetPreStep(string displayName)
        {
            var preStep = FlowDefinition.Steps.FirstOrDefault(c => c.DisplayName == FlowStep.DisplayName);
            if (preStep is null)
            {
                throw new Exception("未找到步骤");
            }
            FlowStep.WaitEvents.Add(new FlowEvent { StepId = preStep.Id, Type = EventType.PreStep });
            return this;
        }

        public IInputCreater<double, T> SetTimeout()
        {
            return new InputCreater<double, T>(FlowDefinition, FlowStep, FlowStep.Timeout);
        }

        public IInputCreater<int, T> SetRetry()
        {
            return new InputCreater<int, T>(FlowDefinition, FlowStep, FlowStep.Retry);
        }

        public IInputCreater<int, T> SetRepeat()
        {
            return new InputCreater<int, T>(FlowDefinition, FlowStep, FlowStep.Repeat);
        }

        public IInputCreater<ErrorHandling, T> SetErrorHandling()
        {
            return new InputCreater<ErrorHandling, T>(FlowDefinition, FlowStep, FlowStep.ErrorHandling);
        }

        public IInputCreater<bool, T> SetFinally()
        {
            return new InputCreater<bool, T>(FlowDefinition, FlowStep, FlowStep.Finally);
        }
    }


    public class InputCreater<T, TStep>(FlowDefinition flowDefinition, FlowStep flowStep, FlowInput flowInput) : StepCreater<TStep>(flowDefinition, flowStep), IInputCreater<T, TStep>
        where TStep : IStep
    {
        public InputCreater(FlowDefinition flowDefinition, FlowStep flowStep, string displayName)
            : this(flowDefinition, flowStep, new FlowInput(displayName))
        {

        }

        public FlowInput FlowInput { get; } = flowInput;
        public IInputCreater<T, TStep> WithValue(string value)
        {
            FlowInput.Mode = InputMode.Normal;
            FlowInput.Value = value;

            return this;
        }

        public IInputCreater<T, TStep> WithEvent(string eventName)
        {
            FlowInput.Mode = InputMode.Event;
            FlowInput.Value = eventName;

            return this;
        }

        public IInputCreater<T, TStep> WithGlobal(string globeName)
        {
            FlowInput.Mode = InputMode.Global;
            FlowInput.Value = globeName;

            return this;
        }


        public IInputCreater<T, TStep> WithArray(params int[] dim)
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

        public IInputArrayCreater<T, TStep> GetArray(params int[] indexes)
        {
            return new InputArrayCreater<T, TStep>(FlowDefinition, FlowStep, FlowInput, indexes);
        }
    }

    public class InputArrayCreater<T, TStep> : InputCreater<T, TStep>, IInputArrayCreater<T, TStep>
       where TStep : IStep
    {
        public FlowInput SubInput { get; }

        public InputArrayCreater(FlowDefinition flowDefinition, FlowStep flowStep, FlowInput flowInput, params int[] indexes) : base(flowDefinition, flowStep, flowInput)
        {
            var indexName = string.Join(",", indexes);

            var subInput = FlowInput.Inputs.FirstOrDefault(c => c.Name == flowInput + $"({indexName})");
            if (subInput is null)
            {
                throw new Exception("未找到对应的数组元素");
            }
            SubInput = subInput;
        }
    }

    public class ConfigCreater
    {
        public ConfigDefinition ConfigDefinition { get; set; }
        public ConfigCreater(string category, string name, string configName)
        {
            ConfigDefinition = new ConfigDefinition(category, name);
            ConfigDefinition.ConfigName = configName;
        }

        public ConfigCreater WithConfigName(string configName)
        {
            ConfigDefinition.ConfigName = configName;
            return this;
        }

        public ConfigCreater WithRetry(int retry)
        {
            ConfigDefinition.Retry = retry;
            return this;
        }

        public ConfigCreater WithRepeat(int repeat)
        {
            ConfigDefinition.Repeat = repeat;
            return this;
        }

        public ConfigCreater WithTimeout(int timeout)
        {
            ConfigDefinition.Timeout = timeout;
            return this;
        }

        public ConfigCreater WithLogView(string logView)
        {
            ConfigDefinition.LogView = logView;
            return this;
        }

        public ConfigCreater WithErrorStop(bool errorStop)
        {
            ConfigDefinition.ErrorStop = errorStop;
            return this;
        }

        public ConfigCreater WithData(string name, string value)
        {
            ConfigDefinition.Data.Add(new Ty.NameValue(name, value));
            return this;
        }

        public ConfigCreater WithFlowMiddlewares(params string[] names)
        {
            ConfigDefinition.FlowMiddlewares.Clear();
            ConfigDefinition.FlowMiddlewares.AddRange(names);
            return this;
        }

        public ConfigCreater WithStepGroupMiddlewares(params string[] names)
        {
            ConfigDefinition.StepGroupMiddlewares.Clear();
            ConfigDefinition.StepGroupMiddlewares.AddRange(names);
            return this;
        }

        public ConfigCreater WithStepMiddlewares(params string[] names)
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
}
