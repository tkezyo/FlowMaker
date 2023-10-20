namespace FlowMaker;

public interface IStep
{
    Task Run();
    Task WrapAsync(Dictionary<string, object> keyValues);
}
