namespace FlowMaker.Persistence;

public interface IFlowProvider
{
    Task<string[]> LoadCategories();
    Task<List<FlowDefinitionListModel>> LoadFlowNamesByCategory(string category);
    Task<List<FlowDefinitionListModel>> LoadFlowAndConfig();

    Task SaveFlow(FlowDefinition flowDefinition);
    Task RemoveFlow(Guid id);
    Task<FlowDefinition?> LoadFlowDefinitionAsync(Guid id);
    Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name);

    Task<ConfigDefinition?> LoadConfigDefinitionAsync(Guid id);
    Task SaveConfig(ConfigDefinition configDefinition);
    Task RemoveConfig(Guid id);
}
