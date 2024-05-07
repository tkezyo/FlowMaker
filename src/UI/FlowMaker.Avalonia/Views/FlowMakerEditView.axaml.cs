using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;

namespace FlowMaker;

public partial class FlowMakerEditView : ReactiveUserControl<FlowMakerEditViewModel>
{
    public FlowMakerEditView()
    {
        InitializeComponent();
    }
}