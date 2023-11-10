using FlowMaker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using ReactiveUI.Validation.Helpers;
using System.Reactive.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Settings;
using Volo.Abp.Users;

namespace FlowMaker.ViewModels
{
    /// <summary>
    /// viewmodel基类
    /// </summary>
    public abstract class ViewModelBase : ReactiveValidationObject, ISingletonDependency
    {
        public IAbpLazyServiceProvider LazyServiceProvider { get; set; } = null!;
        protected TService GetService<TService>() => (TService)ServiceProvider.GetRequiredService(typeof(TService));
        protected TService GetService<TService>(ref TService reference) => GetService(typeof(TService), ref reference);
        protected TRef GetService<TRef>(Type serviceType, ref TRef reference)
        {
            if (reference is null)
            {
                return (TRef)LazyServiceProvider.GetRequiredService(serviceType);
            }
            else
            {
                return reference;
            }
        }
        protected IServiceProvider ServiceProvider => GetService(ref _serviceProvider);
        private IServiceProvider _serviceProvider = null!;

        protected ILoggerFactory LoggerFactory => LazyServiceProvider.LazyGetRequiredService<ILoggerFactory>();
        protected ILogger Logger => LazyServiceProvider.LazyGetService<ILogger>(provider => LoggerFactory?.CreateLogger(GetType().FullName!) ?? NullLogger.Instance);
        protected Type? ObjectMapperContext { get; set; }
        protected IObjectMapper ObjectMapper => LazyServiceProvider.LazyGetService<IObjectMapper>(provider =>
            ObjectMapperContext == null
            ? provider.GetRequiredService<IObjectMapper>()
            : (IObjectMapper)provider.GetRequiredService(typeof(IObjectMapper<>).MakeGenericType(ObjectMapperContext)));

        protected IMessageBoxManager MessageBox => LazyServiceProvider.LazyGetRequiredService<IMessageBoxManager>();
        protected ISettingProvider SettingProvider => LazyServiceProvider.LazyGetRequiredService<ISettingProvider>();

        protected ICurrentUser CurrentUser => GetService(ref _currentUser);
        private ICurrentUser _currentUser = null!;
        protected IAuthorizationService AuthorizationService => GetService(ref _authorizationService);
        private IAuthorizationService _authorizationService = null!;
        protected IStringLocalizerFactory StringLocalizerFactory => LazyServiceProvider.LazyGetRequiredService<IStringLocalizerFactory>();

        protected IStringLocalizer L
        {
            get
            {
                if (_localizer == null)
                {
                    _localizer = CreateLocalizer();
                }

                return _localizer;
            }
        }
        private IStringLocalizer? _localizer;
        protected virtual IStringLocalizer CreateLocalizer()
        {
            if (LocalizationResource != null)
            {
                return StringLocalizerFactory.Create(LocalizationResource);
            }

            var localizer = StringLocalizerFactory.CreateDefaultOrNull();
            if (localizer == null)
            {
                throw new AbpException($"Set {nameof(LocalizationResource)} or define the default localization resource type (by configuring the {nameof(AbpLocalizationOptions)}.{nameof(AbpLocalizationOptions.DefaultResourceType)}) to be able to use the {nameof(L)} object!");
            }

            return localizer;
        }
        protected Type LocalizationResource
        {
            get => _localizationResource;
            set
            {
                _localizationResource = value;
                _localizer = null;
            }
        }
        private Type _localizationResource = typeof(DefaultResource);

        public IFlowMakerRoutableViewModel ShowModal<T>(Action<T>? setValue = null)
           where T : IFlowMakerRoutableViewModel
        {
            var viewModel = GetService<T>();
            setValue?.Invoke(viewModel);
            return viewModel;
        }
        public T Navigate<T>(IScreen screen, Action<T>? setValue = null, string? pageName = null)
          where T : IFlowMakerRoutableViewModel
        {
            var viewModel = GetService<T>();
            viewModel.SetScreen(screen);
            viewModel.UrlPathSegment = pageName;
            setValue?.Invoke(viewModel);
            return viewModel;
        }
        public IFlowMakerRoutableViewModel Navigate(Type viewModelType, IScreen screen, string? pageName = null)
        {
            var viewModel = LazyServiceProvider.LazyGetRequiredService(viewModelType);
            if (viewModel is IFlowMakerRoutableViewModel model)
            {
                model.UrlPathSegment = pageName;
                model.SetScreen(screen);
                return model;
            }
            throw new Exception("未找到视图");
        }
    }

    /// <summary>
    /// 可跳转页面的viewmodel
    /// </summary>
    public abstract class RoutableViewModelBase : ViewModelBase, IFlowMakerRoutableViewModel
    {
        public RoutableViewModelBase()
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
    public interface IFlowMakerRoutableViewModel : IRoutableViewModel, IActivatableViewModel
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
