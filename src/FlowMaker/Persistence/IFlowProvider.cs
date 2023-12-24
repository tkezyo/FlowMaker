using FlowMaker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker.Persistence;

public interface IFlowProvider
{
    Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name);
    string[] LoadCategories();

    IEnumerable<FlowDefinitionFileInfo> LoadFlows(string category);

    Task SaveFlow(FlowDefinition flowDefinition);
    Task RemoveFlow(string category, string name);
    Task<FlowDefinition> LoadFlowDefinitionAsync(string? category, string? name);

    Task SaveConfig(ConfigDefinition configDefinition);
    Task<ConfigDefinition?> LoadConfigDefinitionAsync(string? category, string? name, string configName);
    Task RemoveConfig(string configName, string category, string name);
}
