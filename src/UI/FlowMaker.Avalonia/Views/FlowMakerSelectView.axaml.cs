using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker;

public partial class FlowMakerSelectView : ReactiveUserControl<FlowMakerSelectViewModel>
{
    public FlowMakerSelectView()
    {
        InitializeComponent();
        this.WhenActivated(d => { });
    }
}