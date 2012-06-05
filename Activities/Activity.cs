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
        public string Tag { get; set; }

        public IActivityLog Log { get; set; }

        protected IActivityRuntime Runtime { get; private set; }
        
        protected abstract Task Execute();

        protected void EnsureNotCanceled()
        {
            var runtime = Runtime;
            if (runtime == null)
                return;

            if (runtime.Cancellation.IsCancellationRequested)
                throw new TaskCanceledException("The activity was canceled by the user.");
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