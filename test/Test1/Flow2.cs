using FlowMaker;
using FlowMaker.Models;

namespace Test1
{
    [FlowStep(nameof(Test1), "流程2")]
    public partial class Flow2
    {
        public Task Run(FlowContext context, StepContext stepContext, FlowStep step, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
