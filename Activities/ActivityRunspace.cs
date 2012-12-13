using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public class ActivityRunspace : IActivityRunspace
    {
        private readonly IActivityResolver activityResolver;

        public ActivityRunspace(IActivityResolver activityResolver)
        {
            this.activityResolver = activityResolver;
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
                runtime.Execute();
            });

            return runtime.State;
        }
    }
}