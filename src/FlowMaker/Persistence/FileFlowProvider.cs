using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FlowMaker.Persistence
{
    public class FileFlowProvider : IFlowProvider
    {
        private string FlowDir { get; set; }
        private readonly FlowMakerOption _flowMakerOption;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        public FileFlowProvider(IOptions<FlowMakerOption> options)
        {
            _flowMakerOption = options.Value;
            FlowDir = Path.Combine(_flowMakerOption.FlowRootDir);
            _jsonSerializerOptions = new()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
                IncludeFields = false
            };
        }
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

        public string[] LoadCategories()
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

        public ConcurrentDictionary<string, ConfigDefinition> ConfigDefinitions { get; set; } = [];

        public async Task<ConfigDefinition?> LoadConfigDefinitionAsync(string? category, string? name, string configName)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (ConfigDefinitions.TryGetValue(category + "." + name + "." + configName, out var configDefinition))
            {
                return configDefinition;
            }

            var file = Path.Combine(FlowDir, category, name, $"{configName}" + ".json");
            if (!File.Exists(file))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<ConfigDefinition>(json);
        }

        public ConcurrentDictionary<string, FlowDefinition> FlowDefinitions { get; set; } = [];
        public async Task<FlowDefinition> LoadFlowDefinitionAsync(string? category, string? name)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name))
            {
                throw new Exception($"步骤信息错误:{category},{name}");
            }

            if (FlowDefinitions.TryGetValue(category + "." + name, out var flowDefinition))
            {
                return flowDefinition;
            }

            var file = Path.Combine(FlowDir, category, name + ".json");
            if (!File.Exists(file))
            {
                throw new Exception($"未找到步骤:{category},{name}");
            }

            string json = await File.ReadAllTextAsync(file);
            var df = JsonSerializer.Deserialize<FlowDefinition>(json, _jsonSerializerOptions);
            if (df is null)
            {
                throw new Exception($"步骤文件错误:{category},{name}");
            }

            FlowDefinitions[category + "." + name] = df;

            return df;
        }

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
                var info = new FlowDefinitionFileInfo()
                {
                    Category = category,
                    Name = fileInfo.Name.Replace(".json", ""),
                    CreationTime = fileInfo.CreationTime,
                    ModifyTime = fileInfo.LastWriteTime
                };

                dir = Path.Combine(FlowDir, category, info.Name);
                if (Directory.Exists(dir))
                {
                    info.Configs = Directory.GetFiles(dir, "*.json").Select(c =>
                    {
                        FileInfo fileInfo = new(c);
                        return fileInfo.Name.Replace(".json", "");
                    }).ToList();
                }


                yield return info;
            }
        }

        public Task RemoveFlow(string category, string name)
        {
            var file = Path.Combine(FlowDir, category, name + ".json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            var configDir = Path.Combine(FlowDir, category, name);
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, true);
            }
            return Task.CompletedTask;
        }

        public Task RemoveConfig(string configName, string category, string name)
        {
            var file = Path.Combine(FlowDir, category, name, $"{configName}" + ".json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            return Task.CompletedTask;
        }

        public async Task SaveConfig(ConfigDefinition configDefinition)
        {
            var dir = Path.Combine(FlowDir, configDefinition.Category, configDefinition.Name);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            ConfigDefinitions.TryRemove(configDefinition.Category + "." + configDefinition.Name + "." + configDefinition.ConfigName, out _);

            await File.WriteAllTextAsync(Path.Combine(dir, configDefinition.ConfigName + ".json"), JsonSerializer.Serialize(configDefinition, options: _jsonSerializerOptions));
        }

        public async Task SaveFlow(FlowDefinition flowDefinition)
        {
            if (!Directory.Exists(Path.Combine(FlowDir, flowDefinition.Category)))
            {
                Directory.CreateDirectory(Path.Combine(FlowDir, flowDefinition.Category));
            }


            FlowDefinitions.TryRemove(flowDefinition.Category + "." + flowDefinition.Name, out _);

            await File.WriteAllTextAsync(Path.Combine(FlowDir, flowDefinition.Category, flowDefinition.Name + ".json"), JsonSerializer.Serialize(flowDefinition, options: _jsonSerializerOptions));
        }
    }
}
