using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;
using log4net;

namespace Octopus.Shared.Activities
{
    public class ActivityRuntime : IActivityRuntime
    {
        readonly ActivityState parentState;
        readonly CancellationTokenSource cancellation;

        public ActivityRuntime() : this(null, new CancellationTokenSource())
        {
        }

        private ActivityRuntime(ActivityState parentState, CancellationTokenSource cancellation)
        {
            this.parentState = parentState;
            this.cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation
        {
            get { return cancellation; }
        }

        public Task ExecuteChildren(IEnumerable<IActivity> activities)
        {
            var tasks = activities.Select(ExecuteChild);
            return TaskEx.WhenAll(tasks);
        }

        public async Task ExecuteChild(IActivity activity)
        {
            var log = new StringBuilder();
            using (LogTapper.CaptureTo(log))
            {
                var state = ConfigureChildActivity(activity, log);
                var task = activity.Execute();
                state.Attach(task);
                await task;
            }
        }

        ActivityState ConfigureChildActivity(object activity, StringBuilder log)
        {
            var name = activity.ToString();
            var named = activity as IHaveName;
            if (named != null)
            {
                name = named.Name;
            }

            var childState = new ActivityState(name, log);
            var runtimeAware = activity as IRuntimeAware;
            if (runtimeAware != null)
            {
                runtimeAware.Runtime = new ActivityRuntime(childState, cancellation);
            }

            if (parentState != null)
            {
                parentState.AddChild(childState);
            }

            return childState;
        }

        public static IActivityState BeginExecute(IActivity activity)
        {
            return BeginExecute(activity, null);
        }

        public static IActivityState BeginExecute(IActivity activity, CancellationTokenSource cancellation)
        {
            var runtime = new ActivityRuntime(null, cancellation ?? new CancellationTokenSource());
            var log = new StringBuilder();
            using (LogTapper.CaptureTo(log))
            {
                var state = runtime.ConfigureChildActivity(activity, log);
                var task = activity.Execute();
                state.Attach(task);
                return state;
            }
        }
    }
}