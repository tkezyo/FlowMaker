using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public interface ILogViewModel : ILogInjectViewModel, ITyRoutableViewModel
{
    static abstract string Name { get; }
    Task Load(Guid id);
}
public interface ILogInjectViewModel
{
}