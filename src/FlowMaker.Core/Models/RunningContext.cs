namespace FlowMaker.Models
{
    public class RunningContext
    {
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
        public CancellationToken CancellationToken { get; set; }
    }
}
