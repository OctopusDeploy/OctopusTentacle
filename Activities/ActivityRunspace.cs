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
            var root = new ActivityState(() => "Execute task", null, Guid.NewGuid().ToString(), new CancellationTokenSource());
            var task = new Task(delegate
            {
                var runtime = new ActivityRuntime(activity, null, new CancellationTokenSource(), new ActivityLog(), activityResolver, TaskFactory);
                var childTask = runtime.Execute();
                root.AddChild(runtime.State);
                childTask.Wait();
            });
            root.Attach(task);
            task.Start();
            return root;
        }
    }
}