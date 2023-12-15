using FlowMaker;
using FlowMaker.Models;
using Microsoft.Extensions.Logging;

namespace Test1
{
    public partial class TestFlow1(ILogger<TestFlow1> logger) : IStep
    {
        private readonly ILogger<TestFlow1> _logger = logger;

        public static string Category => "测试步骤";

        public static string Name => "Test1";

        public async Task Run(FlowContext context, StepContext stepContext, FlowStep step, CancellationToken cancellationToken)
        {
            await Task.Delay(5000, cancellationToken);
            _logger.LogInformation(stepContext.DisplayName);
        }
    }
}
