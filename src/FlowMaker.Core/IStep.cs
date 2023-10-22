using FlowMaker.Models;

namespace FlowMaker;

public interface IStep
{
    Task Run(RunningContext context, Step step);
    Task WrapAsync(RunningContext context, Step step);
}
public interface ICheckStep
{
    Task<bool> Run(RunningContext context, Step step);
    Task<bool> WrapAsync(RunningContext context, Step step);
}
