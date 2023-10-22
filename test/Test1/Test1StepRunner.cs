using FlowMaker;
using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Test1
{
    public class Test1StepRunner : IRunner
    {
        public string Name => "Test1";

        private readonly IServiceProvider _serviceProvider;

        public Test1StepRunner(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
        }
        /// <summary>
        /// 获取所有步骤信息，需要包含参数信息
        /// </summary>
        /// <returns></returns>
        public List<string> GetStepInfo()
        {
            return new List<string>();
        }
        public List<string> GetCheckInfo()
        {
            return new List<string>();
        }
        /// <summary>
        /// 执行步骤
        /// </summary>
        /// <param name="stepName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public async Task RunAsync(string stepName, RunningContext context)
        {
            IStep step;
            switch (stepName)
            {
                case "":
                    step = _serviceProvider.GetRequiredService<Flow1>();
                    await step.WrapAsync(context);
                    break;
                default:
                    break;
            }

        }

        public async Task<bool> CheckAsync(string stepName, RunningContext context)
        {
            IStep step;
            switch (stepName)
            {
                case "":
                    step = _serviceProvider.GetRequiredService<Flow1>();
                    await step.WrapAsync(context);
                    break;
                default:
                    break;
            }
            return true;
        }
    }
}
