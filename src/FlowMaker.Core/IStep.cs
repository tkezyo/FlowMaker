using FlowMaker.Models;

namespace FlowMaker;

public interface IStep
{
    Task Run(RunningContext context, Step step, CancellationToken cancellationToken);
    Task WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken);
}
public interface ICheckStep
{
    Task<bool> Run(RunningContext context, Step step, CancellationToken cancellationToken);
    Task<bool> WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken);
}
public interface IStepValueConverter<Tfrom, Tto>
{
    string Name { get; }
    Tto ConvertTo(Tfrom from);
}