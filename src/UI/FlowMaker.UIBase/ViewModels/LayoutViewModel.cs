using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reactive;
using System.Reactive.Linq;
using Volo.Abp.DependencyInjection;
using Volo.Abp.UI.Navigation;

namespace FlowMaker.ViewModels
{
    public class LayoutViewModel : RoutableViewModelBase, IScreen, ISingletonDependency
    {
        protected readonly IMenuManager _menuManager;
        protected readonly ToolOptions _kHToolOptions;

        public RoutingState Router { get; } = new RoutingState(RxApp.MainThreadScheduler);

        public LayoutViewModel(IMenuManager menuManager, IOptions<ToolOptions> options)
        {
            _menuManager = menuManager;
            _kHToolOptions = options.Value;
            ShowThemeToggle = _kHToolOptions.ShowThemeToggle;
            NaviCommand = ReactiveCommand.Create<ApplicationMenuItem>(Navi);
            ToolCommand = ReactiveCommand.CreateFromTask<string>(ToolExcute);
            UrlPathSegment = "Layout";
            foreach (var item in _kHToolOptions.Tools)
            {
                var tool = new ToolViewModel { DisplayName = item.DisplayName, Name = item.Name, Show = true };
                if (item.Color.HasValue)
                {
                    tool.Color = item.Color.Value;
                }
                if (!string.IsNullOrEmpty(item.Icon))
                {
                    tool.Icon = item.Icon;
                }
                item.Enable?.Subscribe(c =>
                {
                    tool.Enable = c;
                });
                item.Show?.Subscribe(c =>
                {
                    tool.Show = c;
                });
                item.ChangeColor?.Subscribe(c =>
                {
                    if (c.HasValue)
                    {
                        tool.Color = c.Value;
                    }
                    else
                    {
                        tool.Color = Color.Gray;
                    }
                });
                item.ChangeDisplayName?.Subscribe(c =>
                {
                    tool.DisplayName = c;
                });
                item.ChangeIcon?.Subscribe(c =>
                {
                    tool.Icon = c;
                });
                Tools.Add(tool);
            }
            Router.CurrentViewModel.WhereNotNull().Subscribe(c =>
            {
                if (!string.IsNullOrWhiteSpace(c.UrlPathSegment))
                {
                    CurrentPage = c.UrlPathSegment;
                    LoadSubMenu(CurrentPage);
                }
            });
        }
        public override async Task Activate()
        {
            await LoadMenu();
        }
        /// <summary>
        /// 显示切换主题按钮
        /// </summary>
        [Reactive]
        public bool ShowThemeToggle { get; set; }
        /// <summary>
        /// 工具栏
        /// </summary>
        [Reactive]
        public ObservableCollection<ToolViewModel> Tools { get; set; } = new ObservableCollection<ToolViewModel>();
        public ReactiveCommand<string, Unit> ToolCommand { get; }
        public virtual async Task ToolExcute(string nameValue)
        {
            await Task.CompletedTask;
            MessageBus.Current.SendMessage(nameValue, "Tool");
        }

        [Reactive]
        public ObservableCollection<ApplicationMenuItem> Menus { get; set; } = new ObservableCollection<ApplicationMenuItem>();
        [Reactive]
        public ObservableCollection<ApplicationMenuItem> SubMenus { get; set; } = new ObservableCollection<ApplicationMenuItem>();
        protected ApplicationMenu? _applicationMenu;
        public virtual async Task LoadMenu()
        {
            _applicationMenu = await _menuManager.GetMainMenuAsync();
            Menus.Clear();
            foreach (var item in _applicationMenu.Items.OrderBy(c => c.Order))
            {
                Menus.Add(item);
            }
            if (string.IsNullOrEmpty(CurrentPage) && Menus.Any())
            {
                Navi(Menus.First());
            }
        }
        public ReactiveCommand<ApplicationMenuItem, Unit> NaviCommand { get; }

        /// <summary>
        /// 当前页面名称
        /// </summary>
        [Reactive]
        public string? CurrentPage { get; set; }
        public virtual void Navi(ApplicationMenuItem menu)
        {
            if (menu.CustomData.TryGetValue("type", out var type))
            {
                Router.NavigateAndReset.Execute(Navigate((Type)type, this, menu.Name));
            }
            else
            {
                if (menu.Items.Any())
                {
                    Router.NavigateAndReset.Execute(Navigate((Type)menu.Items[0].CustomData["type"], this, menu.Items[0].Name));
                }
                else
                {
                    Router.NavigationStack.Clear();
                }
            }
        }


        public void LoadSubMenu(string menu)
        {
            //如果是主菜单,则加载子菜单
            var m = _applicationMenu?.Items.FirstOrDefault(v => v.Name == menu || v.FindMenuItem(menu) is not null);
            if (m is not null)
            {
                SubMenus.Clear();
                foreach (var item in m.Items.OrderBy(c => c.Order))
                {
                    SubMenus.Add(item);
                }
            }
        }
    }

    public class ToolViewModel : ReactiveObject
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        [Reactive]
        public required string DisplayName { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        [Reactive]
        public required string Name { get; set; }
        /// <summary>
        /// 图标
        /// </summary>
        [Reactive]
        public string? Icon { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [Reactive]
        public bool Enable { get; set; } = true;
        /// <summary>
        /// 显示
        /// </summary>
        [Reactive]
        public bool Show { get; set; } = true;
        [Reactive]
        public Color Color { get; set; } = Color.Gray;
        [Reactive]
        public ObservableCollection<ToolViewModel> Children { get; set; } = new();
    }
}
