using FlowMaker.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

namespace FlowMaker.Views
{
    /// <summary>
    /// FlowMakerEditView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerEditView : ReactiveUserControl<FlowMakerEditViewModel>
    {
        public FlowMakerEditView()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                this.WhenAnyValue(c => c.tree.SelectedItem)
                    .Subscribe(x =>
                    {
                        //绑定 treeView 的 selectedItem, 只实现OneWayToSource, view到 viewModel
                        ViewModel!.FlowStep = x as FlowStepViewModel;
                    }).DisposeWith(d);


                //Observable.FromEventPattern<KeyEventHandler, KeyEventArgs>(
                //     h => this.tree.PreviewKeyDown += h,
                //     h => this.tree.PreviewKeyDown -= h)
                // .Where(e => e.EventArgs.Key == Key.Up)
                // .Subscribe(e =>
                // {
                //     ViewModel?.UpAction();

                //     e.EventArgs.Handled = true; // 防止事件进一步冒泡

                // }).DisposeWith(d);

                //Observable.FromEventPattern<KeyEventHandler, KeyEventArgs>(
                //     h => this.tree.PreviewKeyDown += h,
                //     h => this.tree.PreviewKeyDown -= h)
                //.Where(e => e.EventArgs.Key == Key.Down)
                //.Subscribe(e =>
                //{
                //    ViewModel?.DownAction();
                //    e.EventArgs.Handled = true; // 防止事件进一步冒泡

                //}).DisposeWith(d);

            });

        }
    }
}
