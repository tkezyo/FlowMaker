using FlowMaker.Models;
using Polly;

namespace FlowMaker;

public interface IStep
{
    static abstract string GroupName { get; }
    static abstract string Name { get; }
    static abstract StepDefinition GetDefinition();
}
public interface IExcuteStep : IStep
{
    Task Run(RunningContext context, Step step, CancellationToken cancellationToken);
    Task WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken);
}

public interface ICheckStep : IStep
{
    Task<bool> Run(RunningContext context, Step step, CancellationToken cancellationToken);
    Task<bool> WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken);
}
public interface IStepValueConverter<Tfrom, Tto>
{
    string Name { get; }
    Tto ConvertTo(Tfrom from);
}