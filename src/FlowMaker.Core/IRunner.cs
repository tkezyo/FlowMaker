namespace FlowMaker;

public interface IRunner
{
    string Name { get; }

    List<string> GetStepInfo();
    Task RunAsync(string stepName, IReadOnlyDictionary<string, string> param, CancellationToken cancellationToken);
}