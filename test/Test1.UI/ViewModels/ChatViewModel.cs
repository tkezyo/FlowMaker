using System.Threading.Tasks;
using Ty.ViewModels;
using Ty.ViewModels.CustomPages;

namespace Test1.ViewModels
{
    public partial class ChatViewModel : ViewModelBase, ICustomPageViewModel
    {
        public static string Category => "Chat";

        public static string Name => "Chat";

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }
}
