using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Volo.Abp.DependencyInjection;

namespace FlowMaker.ViewModels
{
    [Dependency(ServiceLifetime.Transient)]
    public class ModalDialogViewModel : ViewModelBase, IScreen
    {
        public RoutingState Router { get; } = new RoutingState();
        [Reactive]
        public IFlowMakerRoutableViewModel? ModalViewModel { get; set; }
        [Reactive]
        public string? Title { get; set; }
        [Reactive]
        public int Width { get; set; }
        [Reactive]
        public int Height { get; set; }

        public virtual void Navigate()
        {
            if (ModalViewModel is not null)
            {
                ModalViewModel.SetScreen(this);
                ModalViewModel.WindowTitle = Title;
                Router.Navigate.Execute(ModalViewModel);
            }
        }
    }
}
