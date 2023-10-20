using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker
{
    public class Runner
    {
        private readonly IServiceProvider _serviceProvider;

        public Runner(IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
        }
        public ConcurrentDictionary<string, object> Context { get; set; } = new ConcurrentDictionary<string, object>();
       


    }
}
