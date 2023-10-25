using ReactiveUI;

namespace FlowMaker.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        public string Title { get; set; } = "Kh";
        public RoutingState Router { get; } = new RoutingState();
    }
}
