using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection.Metadata;

namespace FlowMaker;

public class FlowRunner : IDisposable
{
    private readonly ILogger<FlowRunner> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowManager _flowManager;
    private readonly FlowMakerOption _flowMakerOption;
    /// <summary>
    /// 全局上下文
    /// </summary>
    public FlowContext Context { get; set; } = null!;

    private Subject<ExcuteStep> ExcuteStepSubject { get; } = new();
    private readonly Subject<Unit> _locker = new();
    private CancellationToken _cancellationToken;
    public Guid Id { get; set; } = Guid.NewGuid();
    private CompositeDisposable Disposables { get; set; } = [];
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public ReplaySubject<StepStatusArgs> OnStepChange { get; set; } = new();

    public FlowRunner(IServiceProvider serviceProvider, IOptions<FlowMakerOption> option, FlowManager flowManager)
    {
        this._logger = NullLogger<FlowRunner>.Instance;
        _flowMakerOption = option.Value;
        this._serviceProvider = serviceProvider;
        this._flowManager = flowManager;
        var d = ExcuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(c =>
          {
              var key = c.Type + c.Type switch
              {
                  EventType.Step => c.StepId?.ToString(),
                  EventType.Event => c.EventName,
                  EventType.EventData => c.EventName,
                  EventType.Debug => c.StepId?.ToString() + "Debug",
                  EventType.StartFlow => "",
                  _ => ""
              };
              if (Context.ExcuteStepIds.TryGetValue(key, out var steps))
              {
                  foreach (var item in steps)
                  {
                      var step = Context.FlowDefinition.Steps.First(c => c.Id == item);

                      Context.StepState[item].Waits.RemoveAll(c => c == key);
                      if (c.Type == EventType.EventData)//通过事件传递数据
                      {
                          ChangeStepState(step, StepState.ReceivedEventData);

                          foreach (var input in step.Inputs)
                          {
                              if (input.Mode == InputMode.Event && input.Value == c.EventName)
                              {
                                  input.Value = c.EventData;
                              }
                          }
                      }
                      if (c.Type == EventType.Event)
                      {
                          ChangeStepState(step, StepState.ReceivedEvent);
                      }
                      if (c.Type == EventType.Debug)
                      {
                          ChangeStepState(step, StepState.ReceivedCancelDebug);
                      }
                      if (Context.StepState[item].Waits.Count == 0)
                      {
                          _ = Run(step, _cancellationToken);
                      }
                  }
              }
              if (Context.StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
              {
                  //全部完成
                  ChangeFlowState(RunnerState.Complete);
                  if (TaskCompletionSource is not null)
                  {
                      List<FlowResult> results = [];
                      foreach (var item in Context.FlowDefinition.Data)
                      {
                          if (!item.IsOutput)
                          {
                              continue;
                          }
                          if (Context.Data.TryGetValue(item.Name, out var data))
                          {
                              results.Add(new FlowResult { DisplayName = item.DisplayName, Name = item.Name, Type = item.Type, Value = data.Value });
                          }
                      }

                      TaskCompletionSource.SetResult(results);
                  }
              }

              _locker.OnNext(Unit.Default);
          });
        Disposables.Add(d);
    }


    protected TaskCompletionSource<List<FlowResult>>? TaskCompletionSource { get; set; }
    public RunnerState State { get; protected set; } = RunnerState.Complete;


    private Dictionary<Guid, FlowRunner> SubFlowRunners { get; set; } = [];
    protected async Task RunStep(FlowStep step, StepContext stepContext, CancellationToken cancellationToken)
    {
        var stepDefinition = _flowMakerOption.GetStep(step.Category, step.Name);
        if (stepDefinition is not null)
        {
            var stepObj = _serviceProvider.GetRequiredKeyedService<IStepInject>(stepDefinition.Category + ":" + stepDefinition.Name);
            await stepObj.WrapAsync(Context, stepContext, step, _serviceProvider, cancellationToken);
        }
        else
        {
            var subFlowDefinition = await _flowManager.LoadFlowDefinitionAsync(step.Category, step.Name);
            var flowRunner = _serviceProvider.GetRequiredService<FlowRunner>();

            var config = new ConfigDefinition { Category = string.Empty, Name = string.Empty, FlowCategory = step.Category, FlowName = step.Name };
            foreach (var item in subFlowDefinition.Data)
            {
                if (!item.IsInput)
                {
                    continue;
                }

                var value = await IDataConverterInject.GetValue(step.Inputs.First(v => v.Name == item.Name), _serviceProvider, Context, item.DefaultValue, cancellationToken);
                config.Data.Add(new NameValue(item.Name, value));
            }
            flowRunner.Id = step.Id;
            SubFlowRunners.Add(step.Id, flowRunner);
            flowRunner.OnStepChange.Subscribe(OnStepChange.OnNext);
            var results = await flowRunner.Start(subFlowDefinition, config, Context.FlowIds, cancellationToken);

            foreach (var item in results)
            {
                await IDataConverterInject.SetValue(step.Outputs.First(v => v.Name == item.Name), item.Value, _serviceProvider, Context, cancellationToken);
            }
        }
    }
    protected async Task<bool> CheckStep(Guid convertId, CancellationToken cancellationToken)
    {
        var converter = Context.FlowDefinition.Checkers.FirstOrDefault(c => c.Id == convertId) ?? throw new Exception();
        return await IDataConverter<bool>.GetValue(converter, _serviceProvider, Context, s => bool.TryParse(s, out var r) && r, cancellationToken);
    }
    public async Task<List<FlowResult>> Start(FlowDefinition flowInfo, ConfigDefinition config, Guid[] parentIds, CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is null)
        {
            _cancellationToken = CancellationTokenSource.Token;
        }
        else
        {
            _cancellationToken = cancellationToken.Value;
        }
        if (State != RunnerState.Complete)
        {
            throw new Exception("正在运行中");
        }

        try
        {
            TaskCompletionSource = new TaskCompletionSource<List<FlowResult>>();

            Context = new(flowInfo);
            Context.FlowIds = [.. parentIds, Id];
            Context.InitState();
            ChangeFlowState(RunnerState.Running);

            foreach (var item in flowInfo.Data)//写入globe data
            {
                if (!item.IsInput)
                {
                    continue;
                }
                var value = config.Data.FirstOrDefault(c => c.Name == item.Name);
                if (value is null)
                {
                    continue;
                }
                if (Context.Data.TryGetValue(item.Name, out var data))
                {
                    data.Value = value.Value;
                }
                else
                {
                    Context.Data[item.Name] = new FlowGlobeData(item.Name, item.Type, value.Value);
                }
            }

            ExcuteStepSubject.OnNext(new ExcuteStep
            {
                Type = EventType.StartFlow,
            });

            return await TaskCompletionSource.Task;
        }
        catch (Exception e)
        {
            ChangeFlowState(RunnerState.Error);
            throw;
        }
    }

    protected void ChangeFlowState(RunnerState runnerState)
    {
        State = runnerState;
        _flowManager.OnFlowChange.OnNext(new FlowStatusArgs(Context, State));
    }
    protected void ChangeStepState(FlowStep step, StepState state)
    {
        if (OnStepChange.IsDisposed)
        {
            return;
        }
        OnStepChange.OnNext(new StepStatusArgs(step, state, Context.FlowIds));
    }

    public void SendEventData(string eventName, string eventData)
    {
        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.EventData,
            EventData = eventData,
            EventName = eventName
        });
    }
    public void SendEvent(string eventName)
    {
        ExcuteStepSubject.OnNext(new ExcuteStep
        {
            Type = EventType.Event,
            EventName = eventName
        });
    }
    public void SendDebug(Guid stepId, bool debug)
    {
        if (debug)
        {
            var key = stepId.ToString() + "Debug";
            Context.ExcuteStepIds[key].Add(stepId);
            Context.StepState[stepId].Waits.Add(key);
        }
        else
        {
            ExcuteStepSubject.OnNext(new ExcuteStep
            {
                Type = EventType.Debug,
                StepId = stepId
            });
        }
    }
    protected async Task Run(FlowStep step, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.Now;
        try
        {
            StepContext stepContext = new(step)
            {
                Id = step.Id,
                DisplayName = step.DisplayName
            };

            var repeat = await IDataConverter<int>.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
            for (int i = 0; i < repeat; i++)//重复执行
            {
                stepContext.CurrentIndex = i;
                stepContext.ErrorIndex = 0;
                foreach (var item2 in step.Ifs)
                {
                    var result = await CheckStep(item2.Key, cancellationToken);
                    if (result != item2.Value)
                    {
                        ChangeStepState(step, StepState.Skip);

                        //跳过
                        Context.StepState[step.Id].Complete = true;
                        ExcuteStepSubject.OnNext(new ExcuteStep
                        {
                            Type = EventType.Step,
                            StepId = step.Id,
                        });
                        return;
                    }
                }
                while (true)
                {
                    var result = new StepRunResult()
                    {
                        StartTime = DateTime.Now,
                        Index = i,
                        Inputs = []
                    };
                    Context.StepState[step.Id].Results.Add(result);
                    try
                    {
                        ChangeStepState(step, StepState.Start);
                        var timeOut = await IDataConverter<int>.GetValue(step.Repeat, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);

                        //超时策略
                        if (timeOut > 0)
                        {
                            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(timeOut), Polly.Timeout.TimeoutStrategy.Pessimistic);
                            await timeoutPolicy.ExecuteAsync(async c => await RunStep(step, stepContext, c), cancellationToken);
                        }
                        else
                        {
                            await RunStep(step, stepContext, cancellationToken);
                        }

                        result.Complete = true;
                        result.EndTime = DateTime.Now;
                        ChangeStepState(step, StepState.Complete);
                        break;
                    }
                    catch (Exception e)
                    {
                        stepContext.ErrorIndex++;
                        result.Complete = true;
                        result.EndTime = DateTime.Now;
                        ChangeStepState(step, StepState.Error);

                        var campensateSteps = Context.FlowDefinition.Steps.Where(c => c.Compensate == step.Id);
                        foreach (var campensateStep in campensateSteps)
                        {
                            await RunStep(campensateStep, stepContext, cancellationToken);
                        }
                        var retry = await IDataConverter<int>.GetValue(step.Retry, _serviceProvider, Context, s => int.TryParse(s, out var r) ? r : 0, cancellationToken);
                        if (retry < stepContext.ErrorIndex)
                        {
                            break;
                        }

                        var errorHandling = await IDataConverter<ErrorHandling>.GetValue(step.ErrorHandling, _serviceProvider, Context, s => Enum.TryParse<ErrorHandling>(s, out var r) ? r : ErrorHandling.Skip, cancellationToken);

                        switch (errorHandling)
                        {
                            case ErrorHandling.Skip:
                                break;
                            case ErrorHandling.Suspend:
                                Context.StepState[step.Id].Suspend = true;
                                return;
                            case ErrorHandling.Terminate:
                                TaskCompletionSource?.SetException(e);
                                return;
                            default:
                                break;
                        }
                    }
                }


                stepContext.CurrentIndex++;
            }

            ChangeStepState(step, StepState.AllComplete);

            Context.StepState[step.Id].Complete = true;
            Context.StepState[step.Id].ConsumeTime = DateTime.Now - start;

            //执行下一步
            ExcuteStepSubject.OnNext(new ExcuteStep
            {
                Type = EventType.Step,
                StepId = step.Id,
            });
        }

        catch (Exception e)
        {
            TaskCompletionSource?.SetException(e);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
        OnStepChange.Dispose();
        Disposables.Dispose();
    }
}

public class FlowStatusArgs(FlowContext flowContext, RunnerState runnerState)
{
    public FlowContext FlowContext { get; } = flowContext;
    public RunnerState RunnerState { get; } = runnerState;
}
public class StepStatusArgs(FlowStep step, StepState status, Guid[] flowIds)
{
    public FlowStep Step { get; set; } = step;

    public StepState Status { get; set; } = status;
    public Guid[] FlowIds { get; } = flowIds;
}

public enum StepState
{
    Start,
    Complete,
    AllComplete,
    ReceivedEvent,
    ReceivedEventData,
    ReceivedCancelDebug,
    Error,
    Skip
}

/// <summary>
/// 事件
/// </summary>
public class ExcuteStep
{
    public EventType Type { get; set; }
    public Guid? StepId { get; set; }
    public string? EventName { get; set; }
    public string? EventData { get; set; }
}
/// <summary>
/// 事件类型
/// </summary>
public enum EventType
{
    Step,
    Event,
    EventData,
    Debug,
    StartFlow,
}
/// <summary>
/// 步骤运行状态
/// </summary>
public enum RunnerState
{
    Running,
    Suspend,
    Complete,
    Error,
}
