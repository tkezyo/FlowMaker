using FlowMaker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Data;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml.Linq;

namespace FlowMaker;

public class FlowManager
{
    private const string flowDir = "Flows";
    private const string configDir = "Configs";
    private string FlowDir { get; set; }
    private string ConfigDir { get; set; }
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlowMakerOption _flowMakerOption;


    public Subject<FlowStatusArgs> OnFlowChange { get; set; } = new();

    public FlowManager(IServiceProvider serviceProvider, IOptions<FlowMakerOption> options)
    {
        _jsonSerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
        };
        _flowMakerOption = options.Value;
        FlowDir = Path.Combine(_flowMakerOption.RootDir, flowDir);
        ConfigDir = Path.Combine(_flowMakerOption.RootDir, configDir);
        this._serviceProvider = serviceProvider;
    }

    #region Run
    class RunnerStatus(FlowRunner flowRunner, IServiceScope serviceScope)
    {
        public required string ConfigCategory { get; set; }
        public required string ConfigName { get; set; }
        /// <summary>
        /// 流程的类别
        /// </summary>
        public required string FlowCategory { get; set; }
        /// <summary>
        /// 流程的名称
        /// </summary>
        public required string FlowName { get; set; }

        public IServiceScope ServiceScope { get; set; } = serviceScope;
        public FlowRunner FlowRunner { get; set; } = flowRunner;

    }
    public IEnumerable<FlowRunner> RunningFlows => _status.Values.Select(c => c.FlowRunner);
    private readonly ConcurrentDictionary<Guid, RunnerStatus> _status = [];
    public ISubject<StepStatusArgs> GetStepChange(Guid id)
    {
        return _status[id].FlowRunner.OnStepChange;
    }
    public async Task<Guid> Run(string configCategory, string configName, string flowCategory, string flowName)
    {
        var config = await LoadConfigDefinitionAsync(configCategory, configName, flowCategory, flowName);
        if (config is null)
        {
            throw new InvalidOperationException("未找到配置");
        }
        return await Run(config);
    }
    public async Task<Guid> Run(ConfigDefinition configDefinition, Action<Guid>? SetId = null)
    {
        var flow = await LoadFlowDefinitionAsync(configDefinition.FlowCategory, configDefinition.FlowName);

        var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<FlowRunner>();
        _status[runner.Id] = new RunnerStatus(runner, scope)
        {
            ConfigCategory = configDefinition.Category,
            ConfigName = configDefinition.Name,
            FlowCategory = configDefinition.FlowCategory,
            FlowName = configDefinition.FlowName
        };
        SetId?.Invoke(runner.Id);
        _ = runner.Start(flow, configDefinition, []).ContinueWith(async c =>
        {
            await Dispose(runner.Id);
        });
        return runner.Id;
    }

    public async Task Dispose(Guid id)
    {
        _status[id].ServiceScope.Dispose();
        _status[id].FlowRunner.Dispose();
        while (_status[id].FlowRunner.State == RunnerState.Running)
        {
            await Task.Delay(300);
        }

        _status.Remove(id, out _);
    }
    #endregion

    #region Flow
    public async Task<IStepDefinition?> GetStepDefinitionAsync(string category, string name)
    {
        if (_flowMakerOption.Group.TryGetValue(category, out var group))
        {
            return group.StepDefinitions.FirstOrDefault(c => c.Name == name);
        }
        else
        {
            var file = Path.Combine(FlowDir, category, name + ".json");
            if (!File.Exists(file))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<FlowDefinition>(json);
        }
    }
    public async Task SaveFlow(FlowDefinition flowDefinition)
    {
        if (!Directory.Exists(Path.Combine(FlowDir, flowDefinition.Category)))
        {
            Directory.CreateDirectory(Path.Combine(FlowDir, flowDefinition.Category));
        }

        await File.WriteAllTextAsync(Path.Combine(FlowDir, flowDefinition.Category, flowDefinition.Name + ".json"), JsonSerializer.Serialize(flowDefinition, options: _jsonSerializerOptions));
    }
    /// <summary>
    /// 获取所有流程分类
    /// </summary>
    /// <returns></returns>
    public string[] LoadFlowCategories()
    {
        var dir = Path.Combine(FlowDir);
        if (!Directory.Exists(dir))
        {
            return [];
        }
        var dirs = Directory.GetDirectories(dir).Where(c =>
        {
            return Directory.EnumerateFiles(c).Any();
        }).Select(c => c.Replace(FlowDir + "\\", "")).ToArray();
        return dirs;
    }

    /// <summary>
    /// 获取指定分类下的所有流程
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    public IEnumerable<FlowDefinitionFileInfo> LoadFlows(string category)
    {
        var dir = Path.Combine(FlowDir, category);
        if (!Directory.Exists(dir))
        {
            yield break;
        }
        foreach (var item in Directory.GetFiles(dir, "*.json"))
        {
            FileInfo fileInfo = new(item);
            yield return new FlowDefinitionFileInfo()
            {
                Category = category,
                Name = fileInfo.Name.Replace(".json", ""),
                CreationTime = fileInfo.CreationTime,
                ModifyTime = fileInfo.LastWriteTime
            };
        }
    }

    public void RemoveFlow(string category, string name)
    {
        var file = Path.Combine(FlowDir, category, name + ".json");
        if (File.Exists(file))
        {
            File.Delete(file);
        }

    }

    /// <summary>
    /// 加载流程定义
    /// </summary>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task<FlowDefinition> LoadFlowDefinitionAsync(string? category, string? name)
    {
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
        {
            throw new Exception($"步骤信息错误:{category},{name}");
        }
        var file = Path.Combine(FlowDir, category, name + ".json");
        if (!File.Exists(file))
        {
            throw new Exception($"未找到步骤:{category},{name}");
        }

        string json = await File.ReadAllTextAsync(file);
        var df = JsonSerializer.Deserialize<FlowDefinition>(json);
        if (df is null)
        {
            throw new Exception($"步骤文件错误:{category},{name}");
        }
        return df;
    }
    #endregion

    #region Config
    private string CreateConfigName(ConfigDefinition configDefinition)
    {
        return $"{configDefinition.FlowCategory}--{configDefinition.FlowName}--{configDefinition.Name}";
    }
    public async Task SaveConfig(ConfigDefinition configDefinition)
    {
        if (!Directory.Exists(Path.Combine(ConfigDir, configDefinition.Category)))
        {
            Directory.CreateDirectory(Path.Combine(ConfigDir, configDefinition.Category));
        }

        await File.WriteAllTextAsync(Path.Combine(ConfigDir, configDefinition.Category, CreateConfigName(configDefinition) + ".json"), JsonSerializer.Serialize(configDefinition, options: _jsonSerializerOptions));
    }
    /// <summary>
    /// 获取所有流程分类
    /// </summary>
    /// <returns></returns>
    public string[] LoadConfigCategories()
    {
        var dir = Path.Combine(ConfigDir);
        if (!Directory.Exists(dir))
        {
            return [];
        }
        var dirs = Directory.GetDirectories(dir).Where(c =>
        {
            return Directory.EnumerateFiles(c).Any();
        }).Select(c => c.Replace(ConfigDir + "\\", "")).ToArray();
        return dirs;
    }

    /// <summary>
    /// 获取指定分类下的所有流程
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    public IEnumerable<ConfigDefinitionFileInfo> LoadConfigs(string category)
    {
        var dir = Path.Combine(ConfigDir, category);
        if (!Directory.Exists(dir))
        {
            yield break;
        }
        foreach (var item in Directory.GetFiles(dir, "*.json"))
        {
            FileInfo fileInfo = new(item);
            var name = fileInfo.Name.Replace(".json", "").Split("--");
            if (name.Length < 3)
            {
                continue;
            }
            yield return new ConfigDefinitionFileInfo()
            {
                Category = category,
                FlowCategory = name[0],
                FlowName = name[1],
                Name = name[2],
                CreationTime = fileInfo.CreationTime,
                ModifyTime = fileInfo.LastWriteTime
            };
        }
    }

    public void RemoveConfig(string category, string name, string flowCategory, string flowName)
    {
        var file = Path.Combine(ConfigDir, category, $"{flowCategory}--{flowName}--{name}" + ".json");
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// 加载流程定义
    /// </summary>
    /// <param name="category"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task<ConfigDefinition?> LoadConfigDefinitionAsync(string? category, string? name, string flowCategory, string flowName)
    {
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
        {
            return null;
        }
        var file = Path.Combine(ConfigDir, category, $"{flowCategory}--{flowName}--{name}" + ".json");
        if (!File.Exists(file))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<ConfigDefinition>(json);
    }
    #endregion
}
