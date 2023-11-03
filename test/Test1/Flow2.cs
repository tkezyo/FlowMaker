using FlowMaker;
using FlowMaker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test1
{
    [FlowStep(nameof(Test1), "流程2")]
    public partial class Flow2
    {
        public Task Run(RunningContext context, FlowStep step, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
