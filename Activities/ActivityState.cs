using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Activities
{
    public class ActivityState : IActivityState
    {
        readonly List<ActivityState> children = new List<ActivityState>();
        readonly object sync = new object();
        readonly Func<string> name;
        readonly string id;
        readonly CancellationTokenSource cancellationTokenSource;
        readonly ITrace log = new ActivityLog();
        readonly ManualResetEventSlim hasTask = new ManualResetEventSlim(false);
        Task task;
        
        public ActivityState(Func<string> name, string tag, string id, CancellationTokenSource cancellationTokenSource)
        {
            this.name = name;
            this.id = id;
            this.cancellationTokenSource = cancellationTokenSource;
            Tag = tag;
        }

        public string Name { get { return name(); } }

        public string Tag { get; private set; }

        public string Id { get { return id; } }

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

        public ITrace Log
        {
            get { return log; }
        }

        public Exception Error { get { return task == null ? null : task.Exception; } }

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
            hasTask.Set();
        }

        public bool IsComplete
        {
            get
            {
                return task != null && task.IsCompleted;
            }
        }

        public void Cancel()
        {
            cancellationTokenSource.Cancel();
        }

        public void WaitForComplete()
        {
            if (task == null)
            {
                hasTask.Wait();
            }

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