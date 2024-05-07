using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FlowMaker.ViewModels;

namespace FlowMaker;

public partial class FlowMakerCustomPageView : ReactiveUserControl<FlowMakerCustomPageViewModel>
{
    public FlowMakerCustomPageView()
    {
        InitializeComponent();
    }
}