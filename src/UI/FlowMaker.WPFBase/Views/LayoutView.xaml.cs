using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker.Views;

/// <summary>
/// LayoutView.xaml 的交互逻辑
/// </summary>
public partial class LayoutView : ReactiveUserControl<LayoutViewModel>
{
    public LayoutView()
    {
        InitializeComponent();
        this.WhenActivated(d => { });
    }
   
}
