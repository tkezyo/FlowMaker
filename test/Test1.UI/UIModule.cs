using FlowMaker;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Test1.UI;

[DependsOn(typeof(AbpAutofacModule))]
[DependsOn(typeof(FlowMakerWPFBaseModule))]
public class UIModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<ViewForMatch>(options =>
        {
            options.Add(Test1UIViewLocatorMatcher.Match);
        });
    }
}
