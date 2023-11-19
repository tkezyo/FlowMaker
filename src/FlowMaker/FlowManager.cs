using FlowMaker.Models;
using System.Text.Json;

namespace FlowMaker;

public class FlowManager
{
    public Task Run(FlowDefinition flowInfo, FlowContext? context = null)
    {


        return Task.CompletedTask;
    }

    public string[] LoadFlowCategories()
    {
        var dir = Path.Combine("Flows");
        if (!Directory.Exists(dir))
        {
            return [];
        }
        var dirs = Directory.GetDirectories(dir).Select(c => c.Replace("Flows\\", "")).ToArray();
        return dirs;
    }

    public string[] LoadFlows(string category)
    {
        var dir = Path.Combine("Flows", category);
        if (!Directory.Exists(dir))
        {
            return [];
        }
        var files = Directory.GetFiles(dir, "*.json").Select(c => c.Replace(dir + "\\", "").Replace(".json", "")).ToArray();
        return files;
    }


    public async Task<FlowDefinition?> LoadFlowDefinitionAsync(string? category, string? name)
    {
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
        {
            return null;
        }
        var file = Path.Combine("Flows", category, name + ".json");
        if (!File.Exists(file))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<FlowDefinition>(json);
    }
}
