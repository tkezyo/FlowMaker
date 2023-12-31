using ReactiveUI.Fody.Helpers;
using System.ComponentModel;
using Ty.ViewModels;

namespace FlowMaker.ViewModels
{
    public partial class FlowMakerDebugViewModel : ViewModelBase, ICustomPageViewModel
    {
        public static string Category => "默认";

        public static string Name => "Debug";
        [Input]
        [Description("上的覅为")]
        [Option("sdfwef", "werwer")]  
        [Reactive]
        public string? FlowCategory { get; set; }

        [Input]
        [Reactive]
        public string? FlowName { get; set; }


        [Reactive]
        public MonitorInfoViewModel? Model { get; set; }

  

        public Task Load()
        {
            throw new NotImplementedException();
        }
    }
}
