using FlowMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test1.ViewModels
{
    public class ChatViewModel : ViewModelBase, ISpikeViewModel
    {
        public string Name { get; set; } = "sdfwef";

        public static string ViewName => "Chat";
    }
}
