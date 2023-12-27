using ReactiveUI;
using System;
using System.Reactive.Disposables;
using Ty.ViewModels;

namespace Ty.Views
{
    /// <summary>
    /// ModalDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ModalDialog : ReactiveWindow<ModalDialogViewModel>
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
