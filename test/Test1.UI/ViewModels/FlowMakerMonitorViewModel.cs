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
        [Reactive]
        public ObservableCollection<string> Categories { get; set; } = [];

        public FlowMakerMonitorViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options, IServiceProvider serviceProvider)
        {
            Flows.Add(new());
            Flows.Add(new());
            Flows.Add(new());
            this._flowManager = flowManager;
            this._serviceProvider = serviceProvider;
            _flowMakerOption = options.Value;
            var flows = Flows.ToObservableChangeSet();
            flows.SubscribeMany(c =>
            {
                return c.WhenValueChanged(v => v.Category, notifyOnInitialValue: false).WhereNotNull().Subscribe(v =>
                {
                    c.Definitions.Clear();

                    if (_flowMakerOption.Group.TryGetValue(v, out var group))
                    {
                        foreach (var item in group.StepDefinitions)
                        {
                            c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Step));
                        }
                    }

                    foreach (var item in _flowManager.LoadFlows(v))
                    {
                        c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Flow));
                    }
                    foreach (var item in _flowManager.LoadConfigs(v))
                    {
                        c.Definitions.Add(new DefinitionInfoViewModel(v, item.Name, DefinitionType.Config));
                    }

                });
            }).Subscribe();

            flows.SubscribeMany(c =>
            {
                return c.WhenValueChanged(v => v.Definition, notifyOnInitialValue: false).WhereNotNull().Subscribe(async v =>
                {
                    var stepDefinition = await _flowManager.GetStepDefinitionAsync(v.Category, v.Name);
                    if (stepDefinition is null)
                    {
                        return;
                    }
                    c.Data.Clear();
                    foreach (var item in stepDefinition.Data)
                    {
                        if (item.IsInput)
                        {
                            c.Data.Add(new InputDataViewModel(item.Name, $"{item.DisplayName}({item.Type})", item.Type, item.DefaultValue));
                        }
                    }
                });
            }).Subscribe();
        }

        public override Task Activate()
        {
            foreach (var item in _flowMakerOption.Group)
            {
                if (!Categories.Contains(item.Key))
                {
                    Categories.Add(item.Key);
                }
            }

            foreach (var item in _flowManager.LoadFlowCategories())
            {
                if (!Categories.Contains(item))
                {
                    Categories.Add(item);
                }
            }
            foreach (var item in _flowManager.LoadConfigCategories())
            {
                if (!Categories.Contains(item))
                {
                    Categories.Add(item);
                }
            }
            return Task.CompletedTask;
        }
    }


    public class MonitorInfoViewModel : ReactiveObject
    {
        [Reactive]
        public string? Category { get; set; }

        [Reactive]
        public DefinitionInfoViewModel? Definition { get; set; }
        [Reactive]
        public ObservableCollection<DefinitionInfoViewModel> Definitions { get; set; } = [];
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
