using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;

namespace FlowMaker;

public partial class FlowMakerLogView : ReactiveUserControl<FlowMakerLogViewModel>
{
    public FlowMakerLogView()
    {
        InitializeComponent();
    }
}