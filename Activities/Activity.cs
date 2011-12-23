using System;
using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public abstract class Activity : IActivity, ISpawnChildActivities, IHaveName
    {
        protected Activity()
        {
            Name = GetType().Name;
        }

        protected IActivityRuntime Runtime { get; private set; }

        public string Name { get; set; }

        protected abstract void Execute();

        protected void EnsureNotCancelled()
        {
            var runtime = Runtime;
            if (runtime == null)
                return;

            if (runtime.Cancellation.IsCancellationRequested)
                throw new TaskCanceledException("The activity was cancelled by the user.");
        }

        void IActivity.Execute()
        {
            Execute();
        }

        IActivityRuntime ISpawnChildActivities.Runtime
        {
            get { return Runtime; }
            set { Runtime = value; }
        }
    }
}