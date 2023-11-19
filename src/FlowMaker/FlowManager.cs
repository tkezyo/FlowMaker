using FlowMaker.Models;
using System.Text.Json;

namespace FlowMaker;

public class FlowManager
{
    public Task Run(FlowDefinition flowInfo, FlowContext? context = null)
    {


        return Task.CompletedTask;
    }

    #region Flow
    /// <summary>
    /// 获取所有流程分类
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// 获取指定分类下的所有流程
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    public IEnumerable<FlowDefinitionInfo> LoadFlows(string category)
    {
        var dir = Path.Combine("Flows", category);
        if (!Directory.Exists(dir))
        {
            yield break;
        }
        foreach (var item in Directory.GetFiles(dir, "*.json"))
        {
            FileInfo fileInfo = new FileInfo(item);
            yield return new FlowDefinitionInfo()
            {
                Category = category,
                Name = fileInfo.Name.Replace(".json", ""),
                CreationTime = fileInfo.CreationTime,
                ModifyTime = fileInfo.LastWriteTime
            };
        }
    }

    /// <summary>
    /// 加载流程定义
    /// </summary>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <returns></returns>
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
    #endregion


}
