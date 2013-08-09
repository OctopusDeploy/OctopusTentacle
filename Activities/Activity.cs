using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Activities
{
    [Activity]
    public abstract class Activity<TMessage> : IActivity<TMessage> where TMessage : IActivityMessage
    {
        public ITrace Log { get; set; }
        public IActivityRuntime Runtime { get; set; }

        public abstract Task Execute(TMessage message);
        
        protected void EnsureNotCanceled()
        {
            Runtime.EnsureNotCancelled();
        }

        protected Task StartNew(Action callback)
        {
            var invoker = new StartNewThread(Log, callback);
            return Runtime.TaskFactory.StartNew(invoker.Execute, TaskCreationOptions.AttachedToParent);
        }

        [DebuggerNonUserCode]
        class StartNewThread
        {
            readonly ITrace log;
            readonly Action callback;

            public StartNewThread(ITrace log, Action callback)
            {
                this.log = log;
                this.callback = callback;
            }
            
            [DebuggerNonUserCode]
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
                    var root = ex.GetRootError();
                    log.Error(root, ex.Message);
                    throw new ActivityFailedException("A child activity failed: " + root.Message);
                }
            }
        }
    }
}