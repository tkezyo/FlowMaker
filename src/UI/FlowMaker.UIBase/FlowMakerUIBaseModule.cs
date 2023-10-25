using FlowMaker.ViewModels;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectMapping;
using Volo.Abp.UI.Navigation;

namespace FlowMaker
{
    [DependsOn(
        typeof(AbpObjectMappingModule),
        typeof(AbpAutofacModule),
        typeof(AbpAutoMapperModule),
        typeof(AbpUiNavigationModule)
        )]
    public class FlowMakerUIBaseModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<ViewForMatch>(options =>
            {
                options.Add(UIBase.FlowMakerUIBaseViewLocatorMatcher.Match);
            });
            Configure<PageOptions>(options =>
            {
                options.LayoutPage = typeof(LayoutViewModel);
                options.FirstLoadPage = typeof(LayoutViewModel);
            });
        }
    }
}
