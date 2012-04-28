using System;
using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public abstract class Activity : IActivity, IRuntimeAware, IHaveName
    {
        protected Activity()
        {
            Name = GetType().Name;
        }

        public string Name { get; set; }

        protected IActivityRuntime Runtime { get; private set; }
        
        protected abstract Task Execute();

        protected void EnsureNotCancelled()
        {
            var runtime = Runtime;
            if (runtime == null)
                return;

            if (runtime.Cancellation.IsCancellationRequested)
                throw new TaskCanceledException("The activity was cancelled by the user.");
        }

        IActivityRuntime IRuntimeAware.Runtime
        {
            get { return Runtime; }
            set { Runtime = value; }
        }

        Task IActivity.Execute()
        {
            return Execute();
        }
    }
}