using System;
using System.Threading;

namespace Octopus.Shared.Tasks
{
    public class WorkItem<T>
    {
        readonly Thread thread;
        readonly OctoThreadClosure<T> closure;

        public WorkItem(Thread thread, OctoThreadClosure<T> closure)
        {
            this.thread = thread;
            this.closure = closure;
        }

        public Exception Exception
        {
            get { return closure.Exception; }
        }

        public bool HasCompleted()
        {
            return thread.ThreadState == ThreadState.Stopped;
        }
    }
}