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
public interface IValueConverter<Tfrom, Tto>
{
    Tto ConvertTo(Tfrom from);
}

public class ValueConverter : IValueConverter<string, int>
{
    public int ConvertTo(string from)
    {
        return Convert.ToInt32(from);
    }
}