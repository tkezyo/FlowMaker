using FlowMaker.Persistence.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Ty;

namespace FlowMaker.Persistence.EntityFramework;

public class FlowProvider(FlowMakerDbContext flowMakerDbContext, IOptions<FlowMakerOption> options) : IFlowProvider
{
    private readonly FlowMakerOption _flowMakerOption = options.Value;

    public async Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name)
    {
        if (_flowMakerOption.Group.TryGetValue(category, out var group))
        {
            return group.StepDefinitions.FirstOrDefault(c => c.Name == name);
        }
        else
        {
            var flow = await flowMakerDbContext.Flows.FirstOrDefaultAsync(c => c.Category == category && c.Name == name);

            return JsonSerializer.Deserialize<FlowDefinition?>(flow?.Data ?? string.Empty);
        }
    }

    public async Task<string[]> LoadCategories()
    {
        return await flowMakerDbContext.Flows.Select(c => c.Category!).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArrayAsync();
    }
    public async Task<IEnumerable<FlowDefinitionModel>> LoadFlows(string category)
    {
        var list = await flowMakerDbContext.Flows.Where(c => c.Category == category).ToListAsync();
        return list;
    }

    public async Task<ConfigDefinition?> LoadConfigDefinitionAsync(Guid id)
    {
        var model = await flowMakerDbContext.Configs.FindAsync(id);
        return JsonSerializer.Deserialize<ConfigDefinition>(model?.Data ?? string.Empty);
    }

    public async Task<FlowDefinition?> LoadFlowDefinitionAsync(Guid id)
    {
        var model = await flowMakerDbContext.Flows.FindAsync(id);
        return JsonSerializer.Deserialize<FlowDefinition?>(model?.Data ?? string.Empty);
    }


    public Task RemoveConfig(Guid id)
    {
        flowMakerDbContext.Configs.Remove(new ConfigDefinitionModel { Id = id, Name = string.Empty });
        return flowMakerDbContext.SaveChangesAsync();
    }

    public async Task RemoveFlow(Guid id)
    {
        flowMakerDbContext.Flows.Remove(new FlowDefinitionModel { Id = id, Category = string.Empty, Name = string.Empty });
        await flowMakerDbContext.SaveChangesAsync();
    }

    public async Task SaveConfig(ConfigDefinition configDefinition)
    {
        var flowId = await GetFlowId(configDefinition.Category, configDefinition.Name);

        if (configDefinition.Id.HasValue)
        {
            ConfigDefinitionModel? configDefinitionModel = await flowMakerDbContext.Configs.FindAsync(configDefinition.Id.Value);

            if (configDefinitionModel != null)
            {
                configDefinitionModel.Name = configDefinition.Name;
                configDefinitionModel.FlowId = flowId;
                configDefinitionModel.Data = JsonSerializer.Serialize(configDefinition.Data);

                await flowMakerDbContext.SaveChangesAsync();
            }
        }
        else
        {
            ConfigDefinitionModel configDefinitionModel = new ConfigDefinitionModel
            {
                Id = Guid.NewGuid(),
                Name = configDefinition.Name,
                FlowId = flowId,
                Data = JsonSerializer.Serialize(configDefinition.Data)
            };

            flowMakerDbContext.Configs.Add(configDefinitionModel);

            await flowMakerDbContext.SaveChangesAsync();
        }

    }

    public async Task SaveFlow(FlowDefinition flowDefinition)
    {
        if (flowDefinition.Id.HasValue)
        {
            FlowDefinitionModel? flowDefinitionModel = await flowMakerDbContext.Flows.FindAsync(flowDefinition.Id.Value);

            if (flowDefinitionModel != null)
            {
                flowDefinitionModel.Category = flowDefinition.Category;
                flowDefinitionModel.Name = flowDefinition.Name;
                flowDefinitionModel.Data = JsonSerializer.Serialize(flowDefinition);

                await flowMakerDbContext.SaveChangesAsync();
            }
        }
        else
        {
            flowDefinition.Id = Guid.NewGuid();
            FlowDefinitionModel flowDefinitionModel = new FlowDefinitionModel
            {
                Id = flowDefinition.Id.Value,
                Category = flowDefinition.Category,
                Name = flowDefinition.Name,
                Data = JsonSerializer.Serialize(flowDefinition)
            };

            flowMakerDbContext.Flows.Add(flowDefinitionModel);

            await flowMakerDbContext.SaveChangesAsync();
        }
    }

    public async Task<Guid> GetFlowId(string category, string name)
    {
        var flow = await flowMakerDbContext.Flows.Where(c => c.Category == category && c.Name == name).Select(c => c.Id).FirstOrDefaultAsync();

        if (flow == Guid.Empty)
        {
            throw new Exception("Flow not found");
        }


        return flow;

    }

    public async Task<List<FlowDefinitionListModel>> LoadFlowNamesByCategory(string category)
    {
        var flows = await flowMakerDbContext.Flows.Where(c => c.Category == category).Select(c => new { c.Id, c.Category, c.Name }).ToListAsync();

        List<FlowDefinitionListModel> list = [];
        foreach (var flowDefinition in flows)
        {
            list.Add(new FlowDefinitionListModel
            {
                Id = flowDefinition.Id,
                Category = flowDefinition.Category,
                Name = flowDefinition.Name,
            });
        }

        return list;
    }

    public async Task<List<FlowDefinitionListModel>> LoadFlowAndConfig()
    {
        var flows = await flowMakerDbContext.Flows.Select(c => new { c.Id, c.Category, c.Name }).ToListAsync();
        var configs = await flowMakerDbContext.Configs.Select(c => new { c.Id, c.Name, c.FlowId }).ToListAsync();

        var list = flows.Select(c => new FlowDefinitionListModel
        {
            Id = c.Id,
            Category = c.Category,
            Name = c.Name,
            Configs = configs.Where(v => v.FlowId == c.Id).Select(v => new NameValue<Guid> { Name = v.Name, Value = v.Id }).ToList()
        }).ToList();

        return list;
    }
}
