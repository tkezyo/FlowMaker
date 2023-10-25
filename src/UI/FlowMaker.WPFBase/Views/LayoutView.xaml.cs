using FlowMaker.ViewModels;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;
using Volo.Abp.DependencyInjection;

namespace FlowMaker.Views
{
    /// <summary>
    /// LayoutView.xaml 的交互逻辑
    /// </summary>
    [ExposeServices(typeof(IViewFor<LayoutViewModel>))]
    public partial class LayoutView : ReactiveUserControl<LayoutViewModel>, ITransientDependency
    {
        public LayoutView()
        {
            InitializeComponent();
            this.WhenActivated(d => { });
        }
       
    }
}
