using FlowMaker.Models;

namespace FlowMaker;

public interface IRunner
{
    string Name { get; }

    List<string> GetStepInfo();

    Task RunAsync(Step step, RunningContext context);
    Task<bool> CheckAsync(Step step, RunningContext context);
}