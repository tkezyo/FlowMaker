using ReactiveUI;

namespace FlowMaker.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        public string Title { get; set; } = "FlowMaker";
        public RoutingState Router { get; } = new RoutingState();
    }
}
