using FlowMaker.Models;

namespace FlowMaker;

public interface IStepExcutor
{
    string Name { get; }

    List<string> GetStepInfo();

    Task RunAsync(Step step, RunningContext context, CancellationToken cancellationToken);
    Task<bool> CheckAsync(Step step, RunningContext context, CancellationToken cancellationToken);
}