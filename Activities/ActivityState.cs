using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Activities
{
    public class ActivityState : IActivityState
    {
        readonly StringBuilder log;
        readonly List<ActivityState> children = new List<ActivityState>();
        readonly object sync = new object();
        readonly Func<string> name; 
        Task task;
        
        public ActivityState(Func<string> name, string tag, StringBuilder log)
        {
            Guard.ArgumentNotNull(name, "name");
            Guard.ArgumentNotNull(log, "log");
            this.log = log;
            this.name = name;
            Tag = tag;
        }

        public string Name { get { return name(); } }

        public string Tag { get; private set; }

        public ActivityStatus Status
        {
            get
            {
                if (task == null)
                {
                    return ActivityStatus.Pending;
                }

                if (task.IsFaulted || task.IsCanceled || task.Exception != null) return ActivityStatus.Failed;
                return task.IsCompleted ? ActivityStatus.Success : ActivityStatus.Running;
            }
        }

        public Exception Error { get { return task == null ? null : task.Exception; } }
        public StringBuilder Log { get { return log; } }

        public void AddChild(ActivityState state)
        {
            lock (sync)
            {
                children.Add(state);
            }
        }

        public void Attach(Task runningTask)
        {
            task = runningTask;
        }

        public bool IsComplete
        {
            get
            {
                return task.IsCompleted;
            }
        }

        public void WaitForComplete()
        {
            task.Wait();
        }

        ReadOnlyCollection<IActivityState> IActivityState.Children
        {
            get
            {
                lock (sync)
                {
                    return children.OfType<IActivityState>().ToList().AsReadOnly();
                }
            }
        }
    }
}