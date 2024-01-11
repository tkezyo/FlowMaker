using FlowMaker.ViewModels;
using System;
using System.Threading.Tasks;
using Ty.ViewModels;

namespace Test1.ViewModels;

public partial class CustomLogViewModel : ViewModelBase, ILogViewModel
{
    public Task Load(Guid id)
    {
        return Task.CompletedTask;
    }


}
