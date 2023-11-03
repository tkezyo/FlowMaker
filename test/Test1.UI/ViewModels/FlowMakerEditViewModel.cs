using FlowMaker;
using FlowMaker.Models;
using FlowMaker.ViewModels;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Test1.ViewModels;

public class FlowMakerEditViewModel : RoutableViewModelBase
{
    private readonly FlowMakerOption _flowMakerOption;
    public FlowMakerEditViewModel(IOptions<FlowMakerOption> options)
    {
        _flowMakerOption = options.Value;
        CreateCommand = ReactiveCommand.CreateFromTask(Create);
        CreateGlobeDataCommand = ReactiveCommand.Create(CreateGlobeData);
    }
    [Reactive]
    public string? Category { get; set; }
    [Reactive]
    public string? Name { get; set; }


    #region Steps
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ObservableCollection<FlowStepViewModel> Steps { get; set; } = new();
    public ObservableCollection<FlowStepViewModel> CompensateSteps { get; set; } = new();
    public async Task Create()
    {
        var ok = await MessageBox.Modals.Handle(new FlowMaker.Services.ModalInfo("添加步骤", Navigate<FlowMakerEditStepViewModel>(HostScreen)));
        if (ok)
        {

        }
    }
    #endregion

    #region Checkers
    [Reactive]
    public ObservableCollection<FlowStepInputViewModel> FlowCheckers { get; set; } = new();
    #endregion

    #region GlobeDatas
    [Reactive]
    public ObservableCollection<StepDataDefinitionViewModel> GlobeDatas { get; set; } = new();

    public ReactiveCommand<Unit, Unit> CreateGlobeDataCommand { get; }
    public void CreateGlobeData()
    {
        GlobeDatas.Add(new StepDataDefinitionViewModel());
    }

    #endregion

}

public class StepDataDefinitionViewModel : ReactiveObject
{
    [Reactive]
    public string? Type { get; set; }
    [Reactive]
    public string? Name { get; set; }
    [Reactive]
    public string? DisplayName { get; set; }
    [Reactive]
    public string? DefaultValue { get; set; }

    [Reactive]
    public bool IsFlowInput { get; set; }
    [Reactive]
    public bool IsFlowOutput { get; set; }
    [Reactive]
    public bool IsStepOutput { get; set; }

    [Reactive]
    public string? StepName { get; set; }

    [Reactive]
    public ObservableCollection<FlowStepOptionViewModel> Options { get; set; } = new();
}

