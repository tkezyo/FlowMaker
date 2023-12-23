using DynamicData;
using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Test1.ViewModels
{
    public class FlowMakerMainViewModel : ViewModelBase, IScreen
    {
        public RoutingState Router { get; } = new RoutingState();
        private readonly FlowMakerOption _flowMakerOption;
        private readonly IServiceProvider _serviceProvider;
        private readonly FlowManager _flowManager;

        public FlowMakerMainViewModel(IOptions<FlowMakerOption> options, IServiceProvider serviceProvider, FlowManager flowManager)
        {
            _flowMakerOption = options.Value;
            this._serviceProvider = serviceProvider;
            this._flowManager = flowManager;
            ChangeViewCommand = ReactiveCommand.Create<string>(ChangeView);
            ReloadMenuCommand = ReactiveCommand.Create<(string, IList<MenuItemViewModel>)>(c => InitMenu(c.Item1, c.Item2));
        }
        public ObservableCollection<MenuItemViewModel> Menus { get; set; } = [];

        public CompositeDisposable Disposable { get; set; } = [];
        [Reactive]
        public ObservableCollection<string> RunningFlows { get; set; } = [];
        public override Task Activate()
        {
            ChangeView("流程");

            //_flowManager.OnFlowChange.Subscribe(c =>
            //{
            //    if (c.FlowContext.FlowIds.Length > 1)
            //    {
            //        return;
            //    }
            //    if (c.RunnerState == RunnerState.Running)
            //    {
            //        RunningFlows.Add($"{c.FlowContext.FlowDefinition.Category}:{c.FlowContext.FlowDefinition.Name}");
            //    }
            //    else
            //    {
            //        RunningFlows.Remove($"{c.FlowContext.FlowDefinition.Category}:{c.FlowContext.FlowDefinition.Name}");
            //    }
            //}).DisposeWith(Disposable);
            return Task.CompletedTask;
        }
        public ReactiveCommand<string, Unit> ChangeViewCommand { get; set; }
        public async void ChangeView(string name)
        {
            Menus.Clear();
            switch (name)
            {
                case "流程":
                    {
                        var vm = _serviceProvider.GetRequiredService<FlowMakerListViewModel>();
                        vm.SetScreen(this);
                        await Router.NavigateAndReset.Execute(vm);

                        InitMenu(name, vm.InitMenu());
                    }
                    break;
                case "监控":
                    {
                        var vm = _serviceProvider.GetRequiredService<FlowMakerMonitorViewModel>();
                        vm.SetScreen(this);

                        await Router.NavigateAndReset.Execute(vm);

                        InitMenu(name);
                    }
                    break;
                default:
                    {
                        var vm = _serviceProvider.GetRequiredService<FlowMakerCustomPageViewModel>();
                        vm.SetScreen(this);
                        await Router.NavigateAndReset.Execute(vm);
                        await vm.LoadTabs(name);
                        vm.ReloadMenuCommand = ReloadMenuCommand;
                        InitMenu(name, vm.InitMenu());
                    }
                    break;
            }
        }
        public ReactiveCommand<(string, IList<MenuItemViewModel>), Unit> ReloadMenuCommand { get; }

        public void InitMenu(string name, IList<MenuItemViewModel>? menus = null)
        {
            Menus.Clear();
            switch (name)
            {
                case "流程":
                    {
                        Menus.Add(new MenuItemViewModel("监控") { Command = ChangeViewCommand, CommandParameter = "监控" });
                    }
                    break;
                case "监控":
                    {
                        Menus.Add(new MenuItemViewModel("流程") { Command = ChangeViewCommand, CommandParameter = "流程" });
                    }

                    break;
                default:
                    {
                        Menus.Add(new MenuItemViewModel("流程") { Command = ChangeViewCommand, CommandParameter = "流程" });
                        Menus.Add(new MenuItemViewModel("监控") { Command = ChangeViewCommand, CommandParameter = "监控" });
                    }
                    break;

            }
            foreach (var item in _flowMakerOption.Sections)
            {
                if (item == name)
                {
                    continue;
                }
                Menus.Add(new MenuItemViewModel(item) { Command = ChangeViewCommand, CommandParameter = item });
            }
            if (menus is not null)
            {
                Menus.Add(menus);
            }
        }
    }
}
