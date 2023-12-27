using FlowMaker.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FlowMaker.ViewModels;

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
