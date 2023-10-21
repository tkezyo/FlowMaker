using FlowMaker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace FlowMaker
{
    public class Runner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IRunner> _runners;
        private readonly ILogger<Runner> _logger;

        public Runner(IServiceProvider serviceProvider, IEnumerable<IRunner> runners, ILogger<Runner> logger)
        {
            this._serviceProvider = serviceProvider;
            this._runners = runners;
            this._logger = logger;
        }
        public ConcurrentDictionary<string, string> Context { get; set; } = new ConcurrentDictionary<string, string>();
        TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
        readonly Dictionary<Guid, StepResult> State = new();

        public void RunNext(Guid? preStepId, List<Step> steps, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                taskCompletionSource?.TrySetCanceled(cancellationToken);
                return;
            }
            //获取所有依赖于preActionId的任务
            var list = steps.Where(c =>
            {
                if (preStepId.HasValue)
                {
                    return c.PreActions.Contains(preStepId.Value) && !State[c.Id].Started;
                }
                else
                {
                    return !c.PreActions.Any() && !State[c.Id].Started;
                }
            });

            foreach (var item in list)
            {
                bool canNext = true;
                //确定是否满足条件
                foreach (var preAction in item.PreActions)
                {
                    if (!State[preAction].Complete)
                    {
                        canNext = false;
                        break;
                    }
                }
                if (!canNext)
                {
                    continue;
                }
                //确定是否被取消
                if (cancellationToken.IsCancellationRequested)
                {
                    taskCompletionSource?.TrySetCanceled(cancellationToken);
                    return;
                }
                if (State[item.Id].Started)
                {
                    return;
                }
                State[item.Id].Started = true;
                //执行
                _ = Task.Run(async () =>
                {
                    DateTime start = DateTime.Now;
                    try
                    {
                        var runner = _runners.FirstOrDefault(c => c.Name == item.RunnerName);
                        if (runner is null)
                        {
                            throw new Exception();
                        }

                        //超时策略
                        var timeoutPolicy = Policy.TimeoutAsync(item.TimeOut, Polly.Timeout.TimeoutStrategy.Pessimistic);
                        //重试策略
                        var retryPolicy = Policy.Handle<Exception>().RetryAsync(item.Retry, (exception, retryCount) =>
                          {
                              _logger.LogWarning($"执行步骤{item.Name}失败，重试次数{retryCount}，异常信息{exception.Message}");
                          });
                        //回退策略
                        var fallbackPolicy = Policy.Handle<Exception>().FallbackAsync(async c =>
                          {
                              if (!item.Fallback.HasValue)
                              {
                                  return;
                              }
                              var fallback = steps.FirstOrDefault(c => c.Id == item.Fallback.Value);
                              if (fallback is null)
                              {
                                  return;
                              }
                              await runner.RunAsync(fallback.Name, Context, c);
                          });

                        //组合策略
                        var policyWrap = Policy.WrapAsync(timeoutPolicy, retryPolicy, fallbackPolicy);

                        for (int i = 0; i < item.Repeat; i++)//重复执行
                        {
                            await policyWrap.ExecuteAsync(async c => await runner.RunAsync(item.Name, Context, c), cancellationToken);
                        }


                        State[item.Id].Complete = true;
                        State[item.Id].ConsumeTime = DateTime.Now - start;
                        //执行下一步
                        RunNext(item.Id, steps, cancellationToken);

                        //判断是否全部完成
                        if (State.All(c => c.Value.Complete))
                        {
                            if (taskCompletionSource?.Task.Status != TaskStatus.RanToCompletion)
                            {
                                taskCompletionSource?.SetResult();
                            }
                        }
                    }

                    catch (Exception e)
                    {
                        State[item.Id].ConsumeTime = DateTime.Now - start;

                        OnAction?.Invoke(this, new GanttActionArgs(item, GanttActionStatus.Error));

                        if (e is BusinessException kHWarning)
                        {
                            if (kHWarning.GetLogLevel() > LogLevel.Warning)
                            {
                                TaskCompletionSource?.SetException(kHWarning);
                            }
                            else//如果不是严重错误就继续执行
                            {
                                State[item.Id].Complete = true;
                                State[item.Id].ConsumeTime = DateTime.Now - start;
                                //执行下一步
                                StartNext(workflow, item.Id, actions, cancellationToken);

                                //判断是否全部完成
                                if (State.All(c => c.Value.Complete))
                                {
                                    if (TaskCompletionSource?.Task.Status != TaskStatus.RanToCompletion)
                                    {
                                        TaskCompletionSource?.SetResult();
                                    }
                                }
                            }
                        }
                        else
                        {
                            TaskCompletionSource?.SetException(e);
                        }
                    }
                }, cancellationToken);
            }

            return Task.CompletedTask;
        }

    }
}
