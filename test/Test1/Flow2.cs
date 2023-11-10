using FlowMaker;
using FlowMaker.Models;

namespace Test1;

public partial class Flow2 : IStep
{
    public static string Category => "Test1";

    public static string Name => "双方为";

    public Task Run(FlowContext context, StepContext stepContext, FlowStep step, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}