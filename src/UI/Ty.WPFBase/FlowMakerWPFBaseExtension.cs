using Ty.Views;
using Microsoft.Extensions.DependencyInjection;
using Ty;
using Ty.Services;
using Ty.ViewModels;

namespace Ty;

public static class TyWPFBaseExtension
{
    public static void AddBaseViews(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBoxManager, MessageBoxManager>();

        services.AddSingletonView<LayoutViewModel, LayoutView>();
        services.AddTransientView<ModalDialogViewModel, ModalDialog>();
        services.AddTransientView<PromptDialogViewModel, PromptDialog>();
    }
}
