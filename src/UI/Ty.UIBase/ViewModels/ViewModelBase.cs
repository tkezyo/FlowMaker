using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Validation.Helpers;
using System.Reactive.Linq;
using Ty;

namespace Ty.ViewModels
{
    /// <summary>
    /// 可跳转页面的viewmodel
    /// </summary>
    public abstract class ViewModelBase : ReactiveValidationObject, ITyRoutableViewModel
    {
        public ITyRoutableViewModel ShowModal<T>(Action<T>? setValue = null)
          where T : ITyRoutableViewModel
        {
            var viewModel = TyApp.ServiceProvider.GetRequiredService<T>();
            setValue?.Invoke(viewModel);
            return viewModel;
        }
        public T Navigate<T>(IScreen screen, Action<T>? setValue = null, string? pageName = null)
          where T : ITyRoutableViewModel
        {
            var viewModel = TyApp.ServiceProvider.GetRequiredService<T>();
            viewModel.SetScreen(screen);
            viewModel.UrlPathSegment = pageName;
            setValue?.Invoke(viewModel);
            return viewModel;
        }
        public ITyRoutableViewModel Navigate(Type viewModelType, IScreen screen, string? pageName = null)
        {
            var viewModel = TyApp.ServiceProvider.GetRequiredService(viewModelType);
            if (viewModel is ITyRoutableViewModel model)
            {
                model.UrlPathSegment = pageName;
                model.SetScreen(screen);
                return model;
            }
            throw new Exception("未找到视图");
        }
        public ViewModelBase()
        {
            CloseCommand = ReactiveCommand.Create<bool, bool>(c =>
            {
                CloseStatus = c;
                return c;
            }, outputScheduler: RxApp.MainThreadScheduler);
            Activator.Activated.Subscribe(c =>
            {
                MessageBus.Current.SendMessage(UrlPathSegment, "PageActivated");
                Activate();
            });
            Activator.Deactivated.Subscribe(c =>
            {
                Deactivate();
            });
        }
        public bool CloseStatus { get; set; }
        public IScreen HostScreen { get; private set; } = null!;
        public ReactiveCommand<bool, bool> CloseCommand { get; }
        public void CloseModal(bool result = false)
        {
            Observable.Return(result).InvokeCommand(CloseCommand);
        }
        public void SetScreen(IScreen screen)
        {
            HostScreen = screen;
        }

        public virtual Task Activate()
        {
            return Task.CompletedTask;
        }

        public virtual Task Deactivate()
        {
            return Task.CompletedTask;
        }

        public string? UrlPathSegment { get; set; }
        public string? WindowTitle { get; set; }

        public ViewModelActivator Activator { get; set; } = new ViewModelActivator();
    }
    /// <summary>
    /// 可跳转页面的接口
    /// </summary>
    public interface ITyRoutableViewModel : IRoutableViewModel, IActivatableViewModel
    {
        void SetScreen(IScreen screen);
        ReactiveCommand<bool, bool> CloseCommand { get; }
        bool CloseStatus { get; set; }
        /// <summary>
        /// 当前window的标签
        /// </summary>
        string? WindowTitle { get; set; }
        /// <summary>
        /// 激活
        /// </summary>
        /// <returns></returns>
        Task Activate();
        /// <summary>
        /// 隐藏
        /// </summary>
        /// <returns></returns>
        Task Deactivate();

        new string? UrlPathSegment { get; set; }
    }

}
