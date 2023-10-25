using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace FlowMaker;

public class ViewForMatch
{
    private List<Func<object, IViewFor?>> match = new List<Func<object, IViewFor?>>();
    public IReadOnlyList<Func<object, IViewFor?>> Match => match;

    public void Add(Func<object, IViewFor?> func)
    {
        match.Insert(0, func);
    }

    public static IViewFor? GetAndSet<TViewModel>(TViewModel vm)
              where TViewModel : class
    {
        var view = FlowMakerApp.ServiceProvider.GetRequiredService<IViewFor<TViewModel>>();

        return view;
    }
    public static IViewFor? GetAndSet<TViewModel>()
          where TViewModel : class
    {
        var view = FlowMakerApp.ServiceProvider.GetRequiredService<IViewFor<TViewModel>>();

        return view;
    }
}
