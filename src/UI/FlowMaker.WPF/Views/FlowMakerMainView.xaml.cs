using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker.Views
{
    /// <summary>
    /// FlowMakerMainView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerMainView :ReactiveUserControl<FlowMakerMainViewModel>
    {
        public FlowMakerMainView()
        {
            InitializeComponent();
            this.WhenActivated(d => { });
        }
    }
}
