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
            {
                var log = Log;
                if (log != null)
                    Log.Error("The activity was canceled.");

                throw new TaskCanceledException("The activity was canceled.");
            }
        }

        protected Task StartNew(Action callback)
        {
            var invoker = new NewThread(Log, callback);
            return Task.Factory.StartNew(invoker.Execute);
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

        class NewThread
        {
            readonly IActivityLog log;
            readonly Action callback;

            public NewThread(IActivityLog log, Action callback)
            {
                this.log = log;
                this.callback = callback;
            }

            public void Execute()
            {
                try
                {
                    callback();
                }
                catch (ActivityFailedException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    throw new ActivityFailedException("A child activity failed: " + ex.Message);
                }
            }
        }
    }
}