using FlowMaker.Models;

namespace FlowMaker;

public interface IStepProvider
{
    string Name { get; }

    List<StepDefinition> GetStepDefinitions();
    List<StepDefinition> GetCheckStepDefinitions();
    List<ConvertorDefinition> GetConvertors();

    Task RunAsync(Step step, RunningContext context, CancellationToken cancellationToken);
    Task<bool> CheckAsync(Step step, RunningContext context, CancellationToken cancellationToken);
}