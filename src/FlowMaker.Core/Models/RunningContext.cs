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
        public List<FlowStep> AllSteps { get; set; } = new();
        public List<FlowConverter> AllConverters { get; set; } = new();

        /// <summary>
        /// 批量设置输入,将所有步骤的输入设置为相同的值
        /// </summary>
        public Dictionary<string, FlowInput> Inputs { get; set; } = new();
    }

    public class FlowGlobeParam
    {
        public string Name { get; set; }
    }
}
