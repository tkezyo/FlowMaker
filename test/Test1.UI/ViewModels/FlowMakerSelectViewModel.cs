﻿using FlowMaker;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace Test1.ViewModels
{
    public class FlowMakerSelectViewModel : ViewModelBase
    {
        private readonly FlowManager _flowManager;
        private readonly FlowMakerOption _flowMakerOption;

        /// <summary>
        /// Represents a view model for selecting flow makers.
        /// </summary>
        public FlowMakerSelectViewModel(FlowManager flowManager, IOptions<FlowMakerOption> options)
        {
            this._flowManager = flowManager;
            _flowMakerOption = options.Value;
            SaveCommand = ReactiveCommand.Create(Save);
            this.WhenAnyValue(c => c.Category).WhereNotNull().Subscribe(c =>
            {
                Definitions.Clear();

                if (_flowMakerOption.Group.TryGetValue(c, out var group))
                {
                    foreach (var item in group.StepDefinitions)
                    {
                        Definitions.Add(new DefinitionInfoViewModel(c, item.Name, DefinitionType.Step));
                    }
                }

                foreach (var item in _flowManager.LoadFlows(c))
                {
                    Definitions.Add(new DefinitionInfoViewModel(c, item.Name, DefinitionType.Flow));
                }
                foreach (var item in _flowManager.LoadConfigs(c))
                {
                    Definitions.Add(new DefinitionInfoViewModel(c, item.Name, DefinitionType.Config));
                }
            });
        }
        [Reactive]
        public string? DisplayName { get; set; }
        [Reactive]
        public string? Category { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        [Reactive]
        public DefinitionInfoViewModel? Definition { get; set; }
        [Reactive]
        public ObservableCollection<string> Categories { get; set; } = [];
        [Reactive]
        public ObservableCollection<DefinitionInfoViewModel> Definitions { get; set; } = [];
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

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public void Save()
        {
            if (string.IsNullOrEmpty(Category) || Definition is null)
            {
                CloseModal(false);
            }
            else
            {
                CloseModal(true);
            }

        }
    }

    public class DefinitionInfoViewModel(string category, string name, DefinitionType type) : ReactiveObject
    {
        [Reactive]
        public string DisplayName { get; set; } = $"{name} ({type})";
        [Reactive]
        public string Category { get; set; } = category;

        [Reactive]
        public string Name { get; set; } = name;
        [Reactive]
        public DefinitionType Type { get; set; } = type;

    }
    public enum DefinitionType
    {
        Step,
        Flow,
        Config
    }
}
