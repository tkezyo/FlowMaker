using Volo.Abp.Modularity;

namespace FlowMaker
{
    [DependsOn(typeof(FlowMakerUIBaseModule))]
    public class FlowMakerWPFBaseModule : AbpModule
    {
    }
}
