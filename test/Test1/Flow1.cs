using FlowMaker;

namespace Test1
{
    public class Flow1 : IStep
    {
        [Input("")]
        public int Prop1 { get; set; }
        [Input("")]
        public int Prop2 { get; set; }
        [Output("")]
        public int Prop3 { get; set; }

        /// <summary>
        /// 这个要自动生成
        /// </summary>
        /// <param name="keyValues"></param>
        /// <returns></returns>
        public async Task WrapAsync(Dictionary<string, object> keyValues)
        {
            Prop1 = (int)keyValues[""];
            Prop2 = (int)keyValues[""];
            await Run();
            keyValues[""] = Prop3;
        }
        /// <summary>
        /// 执行的命令
        /// </summary>
        /// <returns></returns>
        public Task Run()
        {
            return Task.CompletedTask;
        }
    }
}