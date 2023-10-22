using FlowMaker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace FlowMaker
{
    public class Runner
    {
        private readonly IEnumerable<IRunner> _runners;
        private readonly ILogger<Runner> _logger;

        public Runner(IEnumerable<IRunner> runners)
        {
            this._runners = runners;
            this._logger = NullLogger<Runner>.Instance;
        }
        /// <summary>
        /// 全局上下文
        /// </summary>
        public RunningContext Context { get; protected set; } = new RunningContext();
        /// <summary>
        /// 所有步骤的状态
        /// </summary>
        public Dictionary<Guid, StepResult> StepState { get; protected set; } = new();
        public List<Guid> SuspendSteps { get; protected set; } = new();
        /// <summary>
        /// 所有步骤
        /// </summary>
        public List<Step> AllSteps { get; protected set; } = new();
        public async Task RunStep(Step step)
        {
            var runner = _runners.FirstOrDefault(c => c.Name == step.RunnerName);
            if (runner is null)
            {
                throw new Exception();
            }
            await runner.RunAsync(step, Context);
        }
        public async Task RunStep(Guid stepId)
        {
            var step = AllSteps.FirstOrDefault(c => c.Id == stepId);
            if (step is null)
            {
                throw new Exception();
            }

            await RunStep(step);
        }

        public async Task<bool> CheckStep(Guid stepId)
        {
            var step = AllSteps.FirstOrDefault(c => c.Id == stepId);
            if (step is null)
            {
                throw new Exception();
            }

            var runner = _runners.FirstOrDefault(c => c.Name == step.RunnerName);
            if (runner is null)
            {
                throw new Exception();
            }
            return await runner.CheckAsync(step, Context);
        }
        public CancellationTokenSource? CancellationTokenSource { get; protected set; }
        public RunnerState State { get; set; }
        public void Run(FlowInfo flowInfo)
        {
            if (State != RunnerState.Stop)
            {
                throw new Exception("正在运行中");
            }
            State = RunnerState.Running;
            if (CancellationTokenSource is not null)
            {
                CancellationTokenSource?.Cancel();
            }
            CancellationTokenSource = new CancellationTokenSource();
            //初始化
            foreach (var item in flowInfo.Steps)
            {
                StepState.Add(item.Id, new StepResult());
            }
            AllSteps = flowInfo.Steps;
            ////设置输入
            //foreach (var item in flowInfo.Inputs)
            //{
            //    Context.Data.Add(item.Name, item.Value);
            //}
            ////设置输出
            //foreach (var item in flowInfo.Outputs)
            //{
            //    Context.Data.Add(item.Name, item.Value);
            //}
            //设置全局输入
            foreach (var item in flowInfo.SetInputs)
            {
                Context.Data.Add(item.Key, item.Value);
            }
            //执行
            RunNext(null, CancellationTokenSource.Token);
        }
        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            if (CancellationTokenSource is not null)
            {
                CancellationTokenSource?.Cancel();
            }
            State = RunnerState.Stop;
        }
        /// <summary>
        /// 恢复
        /// </summary>
        public void Resume()
        {

        }
        /// <summary>
        /// 暂停
        /// </summary>
        /// <param name="stepId"></param>
        public void Suspend(Guid stepId)
        {
            if (StepState[stepId].Suspend)
            {
                return;
            }
            StepState[stepId].Suspend = true;
            SuspendSteps.Add(stepId);
        }

        /// <summary>
        /// 检查是否全部完成
        /// </summary>
        public void CheckComplete()
        {
            if (StepState.All(c => c.Value.Complete) && State == RunnerState.Running)
            {
                //全部完成
                State = RunnerState.Stop;
            }
        }

        public void RunNext(Guid? preStepId, CancellationToken cancellationToken)
        {
            //获取所有依赖于preActionId的任务
            var list = AllSteps.Where(c =>
            {
                if (preStepId.HasValue)
                {
                    return c.PreActions.Contains(preStepId.Value) && !StepState[c.Id].Started;
                }
                else
                {
                    return !c.PreActions.Any() && !StepState[c.Id].Started;
                }
            });

            foreach (var item in list)
            {
                bool canNext = true;
                //确定是否满足条件
                foreach (var preAction in item.PreActions)
                {
                    if (!StepState[preAction].Complete)
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
                    return;
                }
                if (StepState[item.Id].Started)//已经开始执行了，不再执行
                {
                    return;
                }
                if (StepState[item.Id].Suspend)//已经暂停了，不再执行
                {
                    return;
                }
                StepState[item.Id].Started = true;
                //执行
                _ = Run(item, cancellationToken);
            }
        }

        private async Task Run(Step step, CancellationToken cancellationToken)
        {
            DateTime start = DateTime.Now;
            try
            {
                //超时策略
                var timeoutPolicy = Policy.TimeoutAsync(step.TimeOut, Polly.Timeout.TimeoutStrategy.Pessimistic);
                //重试策略
                var retryPolicy = Policy.Handle<Exception>().RetryAsync(step.Retry, (exception, retryCount) =>
                {
                    _logger.LogWarning($"执行步骤{step.Name}失败，重试次数{retryCount}，异常信息{exception.Message}");
                });
                //回退策略
                var fallbackPolicy = Policy.Handle<Exception>().FallbackAsync(async c =>
                {
                    if (!step.Compensate.HasValue)
                    {
                        return;
                    }

                    await RunStep(step.Compensate.Value);
                });

                //组合策略
                var policyWrap = Policy.WrapAsync(timeoutPolicy, retryPolicy, fallbackPolicy);

                for (int i = 0; i < step.Repeat; i++)//重复执行
                {
                    foreach (var item2 in step.CanExcute)
                    {
                        var result = await CheckStep(item2.Key);
                        if (result != item2.Value)
                        {
                            continue;
                        }
                    }
                    try
                    {
                        await policyWrap.ExecuteAsync(async c => await RunStep(step), cancellationToken);
                        StepState[step.Id].CompleteTimes++;
                    }
                    catch (Exception e)
                    {
                        StepState[step.Id].ErrorTimes++;
                        switch (step.ErrorHandling)
                        {
                            case ErrorHandling.Skip:
                                break;
                            case ErrorHandling.Suspend:
                                StepState[step.Id].Suspend = true;
                                return;
                            case ErrorHandling.Terminate:
                                throw new Exception("异常停止", e);
                            default:
                                break;
                        }
                    }

                }


                StepState[step.Id].Complete = true;
                StepState[step.Id].ConsumeTime = DateTime.Now - start;

                CheckComplete();
                //执行下一步
                RunNext(step.Id, cancellationToken);
            }

            catch (Exception e)
            {
                StepState[step.Id].ConsumeTime = DateTime.Now - start;

            }
        }
    }

    public enum RunnerState
    {
        Running,
        Suspend,
        Stop
    }
}
