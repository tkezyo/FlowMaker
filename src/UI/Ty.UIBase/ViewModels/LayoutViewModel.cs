using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reactive;
using System.Reactive.Linq;
using Ty;

namespace Ty.ViewModels
{
    public class LayoutViewModel : ViewModelBase, IScreen
    {
        protected readonly ToolOptions _kHToolOptions;

        public RoutingState Router { get; } = new RoutingState(RxApp.MainThreadScheduler);

        public LayoutViewModel(IOptions<ToolOptions> options)
        {
            _kHToolOptions = options.Value;
            ShowThemeToggle = _kHToolOptions.ShowThemeToggle;
            ToolCommand = ReactiveCommand.CreateFromTask<string>(ToolExcute);
            UrlPathSegment = "Layout";
            foreach (var item in _kHToolOptions.Tools)
            {
                var tool = new ToolViewModel(item.DisplayName, item.Name) { Show = true };
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
                }
            });
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

        /// <summary>
        /// 当前页面名称
        /// </summary>
        [Reactive]
        public string? CurrentPage { get; set; }

    }

    public class ToolViewModel : ReactiveObject
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        [Reactive]
        public string DisplayName { get; set; }

        public ToolViewModel(string displayName, string name)
        {
            DisplayName = displayName;
            Name = name;
        }

        /// <summary>
        /// 名称
        /// </summary>
        [Reactive]
        public string Name { get; set; }
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
