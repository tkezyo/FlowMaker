namespace FlowMaker.Persistence;

public interface IFlowProvider
{
    string[] LoadCategories();
    IEnumerable<FlowDefinitionFileInfo> LoadFlows(string category);

    Task SaveFlow(FlowDefinition flowDefinition);
    Task RemoveFlow(string category, string name);
    Task<FlowDefinition> LoadFlowDefinitionAsync(string? category, string? name);
    Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name);

    Task<ConfigDefinition?> LoadConfigDefinitionAsync(string? category, string? name, string configName);
    Task SaveConfig(ConfigDefinition configDefinition);
    Task RemoveConfig(string configName, string category, string name);



}
