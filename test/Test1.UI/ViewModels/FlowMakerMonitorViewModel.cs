using DynamicData;
using DynamicData.Binding;
using FlowMaker;
using FlowMaker.Models;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Test1.ViewModels
{
    public class FlowMakerMonitorViewModel : ViewModelBase
    {
        private readonly FlowManager _flowManager;
        private readonly IServiceProvider _serviceProvider;

        [Reactive]
        public int ColCount { get; set; } = 3;
        [Reactive]
        public int RowCount { get; set; } = 1;
        public ObservableCollection<MonitorInfoViewModel> Flows { get; set; } = [];
        private readonly FlowMakerOption _flowMakerOption;
        //[Reactive]
        //public ObservableCollection<string> Categories { get; set; } = [];
        [Reactive]
        public ObservableCollection<ErrorHandling> ErrorHandlings { get; set; } = [];

        public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider)
        {
            this._flowManager = flowManager;
            this._serviceProvider = serviceProvider;
            _flowMakerOption = options.Value;
            foreach (var item in Enum.GetValues<ErrorHandling>())
            {
                ErrorHandlings.Add(item);
            }
            //var flows = Flows.ToObservableChangeSet();
            //flows.SubscribeMany(c =>
            //{
            //    return c.WhenValueChanged(v => v.Category, notifyOnInitialValue: false).WhereNotNull().Subscribe(v =>
            //    {
            //        c.Definitions.Clear();

            //        if (_flowMakerOption.Group.TryGetValue(v, out var group))
            //        {
            //            foreach (var item in group.StepDefinitions)
            //            {
            //                c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Step));
            //            }
            //        }

            //        foreach (var item in _flowManager.LoadFlows(v))
            //        {
            //            c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Flow));
            //        }
            //        foreach (var item in _flowManager.LoadConfigs(v))
            //        {
            //            c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Config));
            //        }

            //    });
            //}).Subscribe();

            //flows.SubscribeMany(c =>
            //{
            //    return c.WhenValueChanged(v => v.Definition, notifyOnInitialValue: false).WhereNotNull().Subscribe(async v =>
            //    {
            //        var stepDefinition = await _flowManager.GetStepDefinitionAsync(v.Category, v.Name);
            //        if (stepDefinition is null)
            //        {
            //            return;
            //        }
            //        c.Data.Clear();
            //        foreach (var item in stepDefinition.Data)
            //        {
            //            if (item.IsInput)
            //            {
            //                c.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
            //            }
            //        }
            //    });
            //}).Subscribe();
        }
        public CompositeDisposable? Disposables { get; set; }

        public override Task Activate()
        {
            Disposables = [];
            var f = _flowManager.OnFlowChange.Subscribe(c =>
            {

            });
            Disposables.Add(f);

            var d = _flowManager.OnStepChange.Subscribe(c =>
            {

            });
            Disposables.Add(d);

            //foreach (var item in _flowMakerOption.Group)
            //{
            //    if (!Categories.Contains(item.Key))
            //    {
            //        Categories.Add(item.Key);
            //    }
            //}

            //foreach (var item in _flowManager.LoadFlowCategories())
            //{
            //    if (!Categories.Contains(item))
            //    {
            //        Categories.Add(item);
            //    }
            //}
            //foreach (var item in _flowManager.LoadConfigCategories())
            //{
            //    if (!Categories.Contains(item))
            //    {
            //        Categories.Add(item);
            //    }
            //}
            return Task.CompletedTask;
        }
        public override Task Deactivate()
        {
            if (Disposables is not null)
            {
                Disposables.Dispose();
                Disposables = null;
            }
            return base.Deactivate();
        }
        /// <summary>
        /// 载入步骤或流程
        /// </summary>
        /// <param name="category"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public async Task Load(string category, string name)
        {
            var stepDefinition = await _flowManager.GetStepDefinitionAsync(category, name);
            if (stepDefinition is not null)
            {
                MonitorInfoViewModel flow = new(category, name, stepDefinition is FlowDefinition ? DefinitionType.Flow : DefinitionType.Step)
                {
                    Category = category,
                    Debug = true,
                };
                
                Flows.Add(flow);

                foreach (var item in stepDefinition.Data)
                {
                    if (item.IsInput)
                    {
                        flow.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
                    }
                }
            }
            else
            {
                throw new Exception("未找到步骤");
            }
        }
    }


    public class MonitorInfoViewModel(string category, string name, DefinitionType type) : ReactiveObject
    {
        [Reactive]
        public Guid? Id { get; set; }
        [Reactive]
        public bool Debug { get; set; }

        [Reactive]
        public string DisplayName { get; set; } = $"{category}:{name} ({type})";
        [Reactive]
        public string Category { get; set; } = category;

        [Reactive]
        public string Name { get; set; } = name;
        [Reactive]
        public DefinitionType Type { get; set; } = type;

        /// <summary>
        /// 重试
        /// </summary>
        [Reactive]
        public int Retry { get; set; }
        /// <summary>
        /// 重复,如果是负数，则一直重复
        /// </summary>
        [Reactive]
        public int Repeat { get; set; }
        /// <summary>
        /// 超时时间
        /// </summary>
        [Reactive]
        public int Timeout { get; set; }
        /// <summary>
        /// 出现错误时处理方式
        /// </summary>
        [Reactive]
        public ErrorHandling ErrorHandling { get; set; }
        [Reactive]
        public ObservableCollection<InputDataViewModel> Data { get; set; } = [];
    }

}
