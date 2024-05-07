using System.Windows;
using System.Windows.Controls;

namespace FlowMaker.Controls
{
    /// <summary>
    /// FlowInputView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowInputView : UserControl
    {
    //    public static readonly DependencyProperty FlowInputProperty =
    //   DependencyProperty.Register(
    //"FlowInput", typeof(FlowStepInputViewModel), typeof(FlowInputView));

    //    public FlowStepInputViewModel FlowInput
    //    {
    //        get { return (FlowStepInputViewModel)GetValue(FlowInputProperty); }
    //        set { SetValue(FlowInputProperty, value); }
    //    }
        public static readonly DependencyProperty EditModeProperty =
    DependencyProperty.Register(
 "EditMode", typeof(bool), typeof(FlowInputView));

        public bool EditMode
        {
            get { return (bool)GetValue(EditModeProperty); }
            set { SetValue(EditModeProperty, value); }
        }

        public FlowInputView()
        {
            InitializeComponent();
        }
    }
}
