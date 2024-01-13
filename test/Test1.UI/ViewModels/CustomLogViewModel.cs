using FlowMaker.ViewModels;
using ReactiveUI.Fody.Helpers;
using System;
using System.Threading.Tasks;
using Ty.ViewModels;

namespace Test1.ViewModels;

public partial class CustomLogViewModel : ViewModelBase, ILogViewModel
{
    public static string Name => "自定义";

    public Task Load(Guid id)
    {
        Id = id;
        return Task.CompletedTask;
    }

    [Reactive]
    public Guid Id { get; set; }
}
