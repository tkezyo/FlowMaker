using FlowMaker.ViewModels;
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

namespace FlowMaker.Controls
{
    /// <summary>
    /// GanttItemView.xaml 的交互逻辑
    /// </summary>
    public partial class GanttItemView : UserControl
    {
        public GanttItemView()
        {
            InitializeComponent();
        }

        //接收Scale 用于计算宽度
        public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register("Scale", typeof(int), typeof(GanttItemView), new PropertyMetadata(1));
        public int Scale
        {
            get { return (int)GetValue(ScaleProperty); }
            set { SetValue(ScaleProperty, value); }
        }

        //接收ChangePreCommand 用于改变前置步骤
        public static readonly DependencyProperty ChangePreCommandProperty = DependencyProperty.Register("ChangePreCommand", typeof(ICommand), typeof(GanttItemView));
        public ICommand ChangePreCommand
        {
            get { return (ICommand)GetValue(ChangePreCommandProperty); }
            set { SetValue(ChangePreCommandProperty, value); }
        }

        //接收Steps 用于显示步骤
        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register("Steps", typeof(ObservableCollection<FlowStepViewModel>), typeof(GanttItemView));
        public ObservableCollection<FlowStepViewModel> Steps
        {
            get
            {
                return (ObservableCollection<FlowStepViewModel>)GetValue(StepsProperty);
            }
            set
            {
                SetValue(StepsProperty, value);
            }
        }

   
    }
}
