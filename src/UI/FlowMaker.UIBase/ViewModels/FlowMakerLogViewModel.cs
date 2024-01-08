using FlowMaker.Models;
using FlowMaker.Persistence;
using ReactiveUI;
using System.Collections.ObjectModel;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public partial class FlowMakerLogViewModel : ViewModelBase
{
    private readonly IFlowLogReader _flowLogReader;

    public static string Category => "Log";

    public static string Name => "Log";

    public Guid? Id { get; set; }

    public string? FlowCategory { get; set; }
    public string? FlowName { get; set; }

    public FlowMakerLogViewModel(IFlowLogReader flowLogReader)
    {
        this._flowLogReader = flowLogReader;
    }

    public ObservableCollection<StepLogViewModel> StepLogs { get; set; } = [];
    public async Task Load(Guid id)
    {
        Id = id;
        var log = await _flowLogReader.GetFlowLog(Id.Value);
        if (log == null)
        {
            return;
        }
        FlowCategory = log.Category;
        FlowName = log.Name;
        foreach (var stepLog in log.StepLogs)
        {
            foreach (var item in stepLog.Value.StepOnceLogs)
            {
                StepLogs.Add(new StepLogViewModel
                {
                    Name = stepLog.Value.StepName,
                    State = item.State.ToString(),
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Inputs = item.Inputs,
                    Outputs = item.Outputs
                });
            }
        }

    }
}

public class StepLogViewModel : ReactiveObject
{
    public required string Name { get; set; }
    public required string State { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<NameValue> Inputs { get; set; } = [];
    public List<NameValue> Outputs { get; set; } = [];
}