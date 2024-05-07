using FlowMaker.ViewModels;
using System;
using System.Threading.Tasks;
using Ty.ViewModels;

namespace Test1.ViewModels
{
    public class LoadingViewModel : ViewModelBase
    {
        public override async Task Activate()
        {
            var vm = Navigate<FlowMakerMainViewModel>(HostScreen);
            await Task.Delay(1000);
            HostScreen.Router.Navigate.Execute(vm);
        }
    }
}
