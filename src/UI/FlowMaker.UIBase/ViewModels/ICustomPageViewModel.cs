using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public interface ICustomPageViewModel : ICustomPageInjectViewModel, ITyRoutableViewModel
{
    /// <summary>
    /// 类别
    /// </summary>
    static abstract string Category { get; }
    /// <summary>
    /// 名称
    /// </summary>
    static abstract string Name { get; }
    static abstract CustomViewDefinition GetDefinition();
    Task WrapAsync(List<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    Task Load();
}
public interface ICustomPageInjectViewModel
{
}


public interface ILogViewModel : ILogInjectViewModel, ITyRoutableViewModel
{
    static abstract string Name { get; }
    Task Load(Guid id);
}
public interface ILogInjectViewModel
{
}