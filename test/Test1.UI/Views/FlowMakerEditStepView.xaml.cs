using ReactiveUI;
using Test1.ViewModels;
using Volo.Abp.DependencyInjection;

namespace Test1.Views
{
    /// <summary>
    /// FlowMakerEditStepView.xaml 的交互逻辑
    /// </summary>
    [ExposeServices(typeof(IViewFor<FlowMakerEditStepViewModel>))]
    public partial class FlowMakerEditStepView : ReactiveUserControl<FlowMakerEditStepViewModel>, ITransientDependency
    {
        public FlowMakerEditStepView()
        {
            InitializeComponent();
        }
    }
}
