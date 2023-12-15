﻿using ReactiveUI;
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
    /// FlowMakerListView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerListView : ReactiveUserControl<FlowMakerListViewModel>
    {
        public FlowMakerListView()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
          
            });
        }
    }
}
