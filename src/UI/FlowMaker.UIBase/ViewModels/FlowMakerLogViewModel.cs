using FlowMaker.Middlewares;
using FlowMaker.Persistence;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Ty.ViewModels;

namespace FlowMaker.ViewModels;

public partial class FlowMakerLogViewModel : ViewModelBase
{
    private readonly IFlowLogger _flowLogReader;
    private readonly FlowManager _flowManager;

    public static string Category => "Log";

    public static string Name => "Log";

    public Guid? Id { get; set; }

    public string? FlowCategory { get; set; }
    public string? FlowName { get; set; }

    public FlowMakerLogViewModel(IFlowLogger flowLogReader, FlowManager flowManager)
    {
        this._flowLogReader = flowLogReader;
        this._flowManager = flowManager;
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
        StepLogs.Clear();
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

    public override async Task Activate()
    {
        if (!Id.HasValue)
        {
            return;
        }
        Disposable = [];
        var mid = _flowManager.GetRunnerService<IStepOnceMiddleware>(Id.Value, "monitor");

        if (mid is MonitorMiddleware monitor)
        {
            monitor.PercentChange.Sample(TimeSpan.FromSeconds(1)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(async c =>
            {
                await Load(Id.Value);
            }).DisposeWith(Disposable);
        }
        await Task.CompletedTask;
    }
    public CompositeDisposable? Disposable { get; set; }
    public override Task Deactivate()
    {
        if (Disposable is not null)
        {
            Disposable.Dispose();
            Disposable = null;
        }
        return base.Deactivate();
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