namespace FlowMaker.Models
{
    public class RunningContext
    {
        /// <summary>
        /// 全局数据
        /// </summary>
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 所有步骤的状态
        /// </summary>
        public Dictionary<Guid, StepResult> StepState { get; protected set; } = new();

        public List<Guid> SuspendSteps { get; protected set; } = new();

        /// <summary>
        /// 所有步骤
        /// </summary>
        public List<Step> AllSteps { get; set; } = new();
    }
}
