using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;

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
            var runtime = new ActivityRuntime(activity, null, new CancellationTokenSource(), new ActivityLog(), activityResolver, TaskFactory);

            TaskFactory.StartNew(() =>
            {
                try
                {
                    runtime.Execute();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            });

            return runtime.State;
        }
    }
}