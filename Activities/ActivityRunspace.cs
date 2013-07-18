using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Activities
{
    public class ActivityRunspace : IActivityRunspace
    {
        private readonly IActivityResolver activityResolver;
        readonly ILog log;

        public ActivityRunspace(IActivityResolver activityResolver, ILog log)
        {
            this.activityResolver = activityResolver;
            this.log = log;
            TaskFactory = Task.Factory;
        }

        public IActivityResolver ActivityResolver
        {
            get { return activityResolver; }
        }

        public TaskFactory TaskFactory { get; set; } 

        public IActivityState StartActivity(IActivityMessage activity)
        {
            var cancelToken = new CancellationTokenSource();
            var runtime = new ActivityRuntime(activity, null, cancelToken, new ActivityLog(), activityResolver, TaskFactory);
            runtime.Start();
            return runtime.State;
        }
    }
}