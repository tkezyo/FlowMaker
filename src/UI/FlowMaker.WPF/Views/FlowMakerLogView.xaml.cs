using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker.Views
{
    /// <summary>
    /// FlowMakerLogView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerLogView : ReactiveUserControl<FlowMakerLogViewModel>
    {
        public FlowMakerLogView()
        {
            InitializeComponent();
            this.WhenActivated(d => { });
        }
    }
}
