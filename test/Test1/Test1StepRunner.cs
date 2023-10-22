using FlowMaker;
using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Test1
{
    public class Test1StepRunner : IStepProvider
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
        public List<StepDefinition> GetStepDefinitions()
        {
            return new List<StepDefinition>();
        }
        public List<StepDefinition> GetCheckStepDefinitions()
        {
            return new List<StepDefinition>();
        }
        public List<ConvertorDefinition> GetConvertors()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 执行步骤
        /// </summary>
        /// <param name="stepType"></param>
        /// <returns></returns>
        public async Task RunAsync(Step stepType, RunningContext context, CancellationToken cancellationToken)
        {
            IStep step;
            switch (stepType.Name)
            {
                case "":
                    step = _serviceProvider.GetRequiredService<Flow1>();
                    await step.WrapAsync(context, stepType, cancellationToken);
                    break;
                default:
                    break;
            }

        }

        public async Task<bool> CheckAsync(Step stepType, RunningContext context, CancellationToken cancellationToken)
        {
            IStep step;
            switch (stepType.Name)
            {
                case "":
                    step = _serviceProvider.GetRequiredService<Flow1>();
                    await step.WrapAsync(context, stepType, cancellationToken);
                    break;
                default:
                    break;
            }
            return true;
        }

     
    }
}
