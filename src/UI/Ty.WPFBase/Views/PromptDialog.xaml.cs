using ReactiveUI;
using System;
using System.Reactive.Disposables;
using Ty.ViewModels;

namespace Ty.Views
{
    /// <summary>
    /// PromptDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PromptDialog : ReactiveWindow<PromptDialogViewModel>
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
