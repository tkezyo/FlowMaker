using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;

namespace FlowMaker.Middlewares;

public class FlowExecuteMiddleware(IServiceProvider serviceProvider) : IMiddleware<FlowContext>
{
    private CancellationToken CancellationToken;

    public async Task InvokeAsync(MiddlewareDelegate<FlowContext> next, FlowContext context, CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        var d = context.ExecuteStepSubject.Zip(_locker.StartWith(Unit.Default)).Select(c => c.First).Subscribe(ExecuteNextStep);
        context.Disposables.Add(d);

        context.ExecuteStepSubject.OnNext(new ExecuteStepEvent
        {
            Type = EventType.StartFlow,
            Context = context
        });

        await next(context, cancellationToken);
    }

    /// <summary>
    /// 锁,防止多线程执行步骤分发
    /// </summary>
    private readonly Subject<Unit> _locker = new();

    public static string Name => "执行";

    /// <summary>
    /// 触发事件后,执行关联步骤
    /// </summary>
    /// <param name="executeStep"></param>
    protected void ExecuteNextStep(ExecuteStepEvent executeStep)
    {
        var key = executeStep.Type + executeStep.Type switch
        {
            EventType.PreStep => executeStep.StepId?.ToString(),
            EventType.Event => executeStep.EventName,
            EventType.StartFlow => "",
            _ => ""
        };
        if (executeStep.Context.ExecuteStepIds.TryGetValue(key, out var steps))
        {
            foreach (var item in steps)
            {
                var stepGroupState = executeStep.Context.StepState.Lookup(item);
                if (stepGroupState.HasValue)
                {
                    stepGroupState.Value.Waits.Remove(key);

                    if (stepGroupState.Value.Waits.Count == 0)
                    {
                        var step = executeStep.Context.FlowDefinition.Steps.First(c => c.Id == item);
                        var builder = new MiddlewareBuilder<StepGroupContext>(serviceProvider);

                        foreach (var mid in executeStep.Context.StepGroupMiddlewares)
                        {
                            builder.Use(mid);
                        }

                        var application = builder.Build();

                        _ = application.Invoke(new StepGroupContext(executeStep.Context, step, stepGroupState.Value), CancellationToken);
                    }
                }
            }
        }

        if (executeStep.Context.StepState.Items.All(c => c.EndTime.HasValue) && executeStep.Context.State == FlowState.Running)
        {
            //全部完成
            if (executeStep.Context.TaskCompletionSource is null || executeStep.Context.TaskCompletionSource.Task.Status == TaskStatus.RanToCompletion)
            {
                return;
            }
            executeStep.Context.TaskCompletionSource.SetResult();
        }

        _locker.OnNext(Unit.Default);
    }
}
