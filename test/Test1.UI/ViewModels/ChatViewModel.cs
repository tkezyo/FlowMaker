using FlowMaker;
using FlowMaker.Fluent;
using System;
using System.Threading.Tasks;
using Ty.ViewModels;
using Ty.ViewModels.CustomPages;

namespace Test1.ViewModels
{
    public partial class ChatViewModel : ViewModelBase, ICustomPageViewModel
    {
        private readonly FlowManager _flowManager;

        public static string Category => "Chat";

        public static string Name => "Chat";

        public ChatViewModel(FlowManager flowManager)
        {
            this._flowManager = flowManager;
        }
        public async Task Load()
        {
            var flow = Test();
            var config = ConfigDefinition();
            var id = await _flowManager.Init(config, flow);
            await foreach (var item in _flowManager.Run(id))
            {

            }

            await Task.CompletedTask;
        }

        public static ConfigDefinition ConfigDefinition()
        {
            return new ConfigCreater("流程分类", "流程1", "")
                .Build();
        }

        public static FlowDefinition Test()
        {
            var r = new FlowCreater("流程分类", "流程1")
                     .NextFlow2("步骤1")
                         .SetInteger()
                         .WithValue(Flow2.Options.Integer.Min1)
                     .NextFlow2("345")
                         .SetInteger().WithValue(Flow2.Options.Integer.Min1)

                         .SetArray(1, 2)
                             .GetArray(1, 2).WithGlobal("")
                             .GetArray(1, 1).WithValue("")

                         .SetArray(1, 2)
                             .GetArray(1, 1).WithValue("")

                         .SetTimeout().WithValue("12")

                         .SetPreStep("123")

                     .NextFlow2("步骤2")
                         .SetInteger().WithValue(Flow2.Options.Integer.Min1)

                         .SetArray(1, 3).WithArray(1, 2)
                         .SetPreStep("123")
                         .SetTimeout().WithValue("12")


                     .Build()
                 ;
            return r;
        }
    }
}
