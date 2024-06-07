using FlowMaker;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Ty;


namespace Test1.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var fm = TyApp.ServiceProvider.GetRequiredService<FlowManager>();
        fm.DisposeAll();
        base.OnExit(e);
    }
}

