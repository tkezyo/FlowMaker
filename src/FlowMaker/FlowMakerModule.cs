using FlowMaker.Models;
using FlowMaker.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker
{

    public class FlowMakerModule : ModuleBase
    {
        public override void DependsOn()
        {

        }
        public override Task ConfigureServices(IServiceCollection serviceDescriptors)
        {
            serviceDescriptors.AddTransient<FlowRunner>();
            serviceDescriptors.AddTransient<IFlowProvider, FileFlowProvider>();
            serviceDescriptors.AddSingleton<FlowManager>();
            return Task.CompletedTask;
        }
    }

    public abstract class ModuleBase : IModule
    {
        public string Name => GetType().Name;
        public Dictionary<string, IModule> Modules { get; set; } = [];

        public abstract Task ConfigureServices(IServiceCollection serviceDescriptors);

        public virtual void DependsOn()
        {
        }

        public void AddDepend<T>()
            where T : IModule, new()
        {
            var t = IModule.AllModules.FirstOrDefault(c => c.Name == typeof(T).Name);
            if (t is null)
            {
                t = new T();
                IModule.AllModules.Add(t);
            }

            Modules.Add(typeof(T).Name, t);
            if (!IModule.SkipVerification && !IModule.Verification(t, this))
            {
                throw new Exception($"模块 {t.Name} 依赖于 {this.Name}，但是 {this.Name} 已经依赖于 {t.Name}，这将导致循环依赖");
            }
            t.DependsOn();
        }
    }

    public interface IModule
    {
        string Name { get; }
        Dictionary<string, IModule> Modules { get; set; }
        void DependsOn();
        Task ConfigureServices(IServiceCollection serviceDescriptors);
        public static bool SkipVerification { get; set; } = true;
        public static List<IModule> AllModules { get; set; } = [];
        public static bool Verification(IModule newPre, IModule source)
        {
            if (newPre.Modules.Any(c => c.Key == source.Name))
            {
                return false;
            }

            foreach (var item in newPre.Modules)
            {
                var action = AllModules.First(c => c.Name == item.Key);

                var r = Verification(action, source);
                if (!r)
                {
                    return false;
                }
            }

            return true;
        }
        public static async Task ConfigureServices<T>(IServiceCollection serviceDescriptors, bool skipVerification = true)
            where T : IModule, new()
        {
            SkipVerification = skipVerification;
            List<ModuleModel> modules = [];

            void SetModules(IModule module)
            {
                ModuleModel moduleModel = new()
                {
                    Module = module,
                };
                foreach (var attr in module.Modules)
                {
                    moduleModel.PreModules.Add(attr.Key);
                    SetModules(attr.Value);
                }
                modules.Add(moduleModel);
            }
            var t = new T();
            t.DependsOn();
            SetModules(t);

            List<ModuleModel> Order(IEnumerable<ModuleModel> modules)
            {
                List<ModuleModel> result = [];

                List<string> total = modules.Select(c => c.Module.Name).ToList();

                List<(string, string)> temp = [];

                foreach (var action in modules)
                {
                    foreach (var item in action.PreModules)
                    {
                        temp.Add((action.Module.Name, item));
                    }
                }

                while (total.Count != 0)
                {
                    var has = temp.Select(c => c.Item2).Distinct().ToList();
                    var ordered = total.Except(has);
                    total = has;
                    temp = temp.Where(c => !ordered.Contains(c.Item1)).ToList();
                    result.AddRange(modules.Where(c => ordered.Contains(c.Module.Name)).ToList());
                }

                return result;
            }
            Order(modules);

            foreach (var item in modules)
            {
                await item.Module.ConfigureServices(serviceDescriptors);
            }
        }
    }
    class ModuleModel
    {
        public List<string> PreModules { get; set; } = [];
        public required IModule Module { get; set; }
    }
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class DependsOnAttribute<T> : Attribute
        where T : IModule
    {
    }
}
