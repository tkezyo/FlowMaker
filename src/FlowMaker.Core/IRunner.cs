namespace FlowMaker;

public interface IRunner
{
    string Name { get; }

    List<string> GetStepInfo();
    Task RunAsync(string stepName, Dictionary<string, object> param);
}