using FlowMaker.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using Volo.Abp.DependencyInjection;

namespace FlowMaker.Views
{
    /// <summary>
    /// ModalDialog.xaml 的交互逻辑
    /// </summary>
    [ExposeServices(typeof(IViewFor<ModalDialogViewModel>))]
    public partial class ModalDialog : ReactiveWindow<ModalDialogViewModel>, ITransientDependency
    {
        public ModalDialog()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                ViewModel!.Navigate();
                ViewModel!.ModalViewModel!.CloseCommand.Subscribe(c =>
                {
                    Close();
                }).DisposeWith(d);
            });
        }
    }
}
