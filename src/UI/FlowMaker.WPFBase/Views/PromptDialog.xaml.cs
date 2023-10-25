using FlowMaker.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using Volo.Abp.DependencyInjection;

namespace FlowMaker.Views
{
    /// <summary>
    /// PromptDialog.xaml 的交互逻辑
    /// </summary>
    [ExposeServices(typeof(IViewFor<PromptDialogViewModel>))]
    public partial class PromptDialog : ReactiveWindow<PromptDialogViewModel>, ITransientDependency
    {
        public PromptDialog()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                ViewModel!.CloseCommand.Subscribe(c =>
                {
                    Close();
                }).DisposeWith(d);
            });
        }
    }
}
