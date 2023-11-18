using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FlowMaker.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IHostApplicationLifetime hostApplicationLifetime, IOptions<PageOptions> options)
        {
            InitializeComponent();
            var vm = new MainWindowViewModel() { Title = "牛马指挥官" };
            DataContext = vm;
            this.hostApplicationLifetime = hostApplicationLifetime;

            RxApp.MainThreadScheduler.Schedule(async () =>
            {
                var login = FlowMakerApp.ServiceProvider.GetRequiredService(options.Value.FirstLoadPage);
                if (login is IFlowMakerRoutableViewModel routableViewModel)
                {
                    routableViewModel.SetScreen(vm);
                    //var login = _abpApplication.ServiceProvider.GetRequiredService<SerialPortViewModel>();
                    await vm.Router.Navigate.Execute(routableViewModel);
                }

            });
        }

        private readonly IHostApplicationLifetime hostApplicationLifetime;

        protected override void OnClosed(EventArgs e)
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}
