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
using Test1.ViewModels;

namespace Test1.Views
{
    /// <summary>
    /// FlowMakerCustomPageView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerCustomPageView : ReactiveUserControl<FlowMakerCustomPageViewModel>
    {
        public FlowMakerCustomPageView()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                Observable.FromEventPattern<KeyEventHandler, KeyEventArgs>(c => KeyDown += c, c => KeyDown -= c).Subscribe(c =>
                {
                    if (ViewModel is null)
                    {
                        return;
                    }

                    if (c.EventArgs.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        switch (c.EventArgs.Key)
                        {
                            case Key.W:
                                ViewModel.AddHeightCommand.Execute(false).Subscribe();
                                break;
                            case Key.S:
                                ViewModel.AddHeightCommand.Execute(true).Subscribe();
                                break;
                            case Key.A:
                                ViewModel.AddWidthCommand.Execute(false).Subscribe();
                                break;
                            case Key.D:
                                ViewModel.AddWidthCommand.Execute(true).Subscribe();
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        switch (c.EventArgs.Key)
                        {
                            case Key.W:
                                ViewModel.TopCommand.Execute(false).Subscribe();
                                break;
                            case Key.S:
                                ViewModel.TopCommand.Execute(true).Subscribe();
                                break;
                            case Key.A:
                                ViewModel.LeftCommand.Execute(false).Subscribe();
                                break;
                            case Key.D:
                                ViewModel.LeftCommand.Execute(true).Subscribe();
                                break;
                            default:
                                break;
                        }
                    }

                }).DisposeWith(d);
            });
        }
    }
}
