using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker;

public partial class FlowMakerMonitorView : ReactiveUserControl<FlowMakerMonitorViewModel>
{
    public FlowMakerMonitorView()
    {
        InitializeComponent();
        this.WhenActivated(d => { });
    }
}