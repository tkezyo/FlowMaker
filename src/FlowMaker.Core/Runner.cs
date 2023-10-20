using FlowMaker.Models;
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
        private readonly IEnumerable<IRunner> _runners;

        public Runner(IServiceProvider serviceProvider, IEnumerable<IRunner> runners)
        {
            this._serviceProvider = serviceProvider;
            this._runners = runners;
        }
        public ConcurrentDictionary<string, object> Context { get; set; } = new ConcurrentDictionary<string, object>();

        public Task Run()
        {
            List<Step> steps = new List<Step>();


            return Task.CompletedTask;
        }

    }
}
