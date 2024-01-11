using FlowMaker.Models;
using FlowMaker.Persistence;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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
        this.WhenAnyValue(c => c.CurrentLog).WhereNotNull().Subscribe(c =>
        {
            Detail = string.Join(",", c.Inputs.Select(d => $"{d.Name}={d.Value}").Concat(c.Outputs.Select(d => $"{d.Name}={d.Value}")));
        });
    }
    [Reactive]
    public string? Detail { get; set; }
    [Reactive]
    public StepLogViewModel? CurrentLog { get; set; }
    public ObservableCollection<StepLogViewModel> StepLogs { get; set; } = [];
    public async Task Load(Guid id)
    {
        Id = id;
        var logs = await _flowLogReader.GetFlowLog(Id.Value);
        foreach (var log in logs)
        {
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
                        Outputs = item.Outputs,
                        StepCurrentIndex = item.CurrentIndex,
                        StepErrorIndex = item.ErrorIndex,
                        FlowCurrentIndex = log.CurrentIndex,
                        FlowErrorIndex = log.ErrorIndex
                    });
                }
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
    public int FlowCurrentIndex { get; set; }
    public int FlowErrorIndex { get; set; }
    public int StepCurrentIndex { get; set; }
    public int StepErrorIndex { get; set; }
    public List<NameValue> Inputs { get; set; } = [];
    public List<NameValue> Outputs { get; set; } = [];
}