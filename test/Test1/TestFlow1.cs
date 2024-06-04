using FlowMaker;
using Microsoft.Extensions.Logging;
using Ty;
using Ty.Module.Configs;

namespace Test1
{
    public partial class TestFlow1(ILogger<TestFlow1> logger) : IStep
    {
        private readonly ILogger<TestFlow1> _logger = logger;

        public static string Category => "测试步骤";

        public static string Name => "Test1";

        public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
        {
            await Task.Delay(5000, cancellationToken);
            _logger.LogInformation(stepContext.Step.DisplayName);
        }
    }

    public partial class PortProvider : IOptionProvider<string>
    {
        public static string DisplayName => "串口";

        public async Task<IEnumerable<NameValue>> GetOptions()
        {
            await Task.CompletedTask;
            return [new("oo", "22"), new("oo22", "2211")];
        }
    }
}
