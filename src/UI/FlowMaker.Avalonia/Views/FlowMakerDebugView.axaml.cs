using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;
using ReactiveUI;

namespace FlowMaker;

public partial class FlowMakerDebugView : ReactiveUserControl<FlowMakerDebugViewModel>
{
    public FlowMakerDebugView()
    {
        InitializeComponent();
        this.WhenActivated(d => { });
    }
}