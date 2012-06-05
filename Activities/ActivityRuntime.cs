using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using log4net;

namespace Octopus.Shared.Activities
{
    public class ActivityRuntime : IActivityRuntime
    {
        readonly ActivityState parentState;
        readonly CancellationTokenSource cancellation;
        readonly IActivityLog log;

        private ActivityRuntime(ActivityState parentState, CancellationTokenSource cancellation, IActivityLog log)
        {
            this.parentState = parentState;
            this.cancellation = cancellation;
            this.log = log;
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
            var state = ConfigureChildActivity(activity);
            var task = activity.Execute();
            state.Attach(task);

            try
            {
                await task;                    
            }
            catch (Exception ex)
            {
                HandleError(ex);
                throw;
            }
        }

        void HandleError(Exception exception)
        {
            exception = exception.GetRootError();
            if (exception is TaskCanceledException)
            {
                log.Error("The task was canceled.");
            }
            else if (exception is ActivityFailedException)
            {
                log.Error(exception.Message);
            }
            else
            {
                log.Error(exception);
            }
        }

        ActivityState ConfigureChildActivity(object activity)
        {
            Func<string> name;
            var tag = string.Empty;
            var named = activity as IHaveName;
            if (named != null)
            {
                name = () => named.Name;
                tag = named.Tag;
            }
            else
            {
                name = activity.ToString;
            }

            var childState = new ActivityState(name, tag);
            var runtimeAware = activity as IRuntimeAware;
            if (runtimeAware != null)
            {
                runtimeAware.Runtime = new ActivityRuntime(childState, cancellation, log);
                runtimeAware.Log = childState.Log;
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
            var runtime = new ActivityRuntime(null, cancellation ?? new CancellationTokenSource(), new NullActivityLog(Logger.Default));
            var logOutput = new StringBuilder();
            
            using (LogTapper.CaptureTo(logOutput))
            {
                var state = runtime.ConfigureChildActivity(activity);
                var task = Task.Factory.StartNew(() =>
                {
                    // Force the activity to run on at least one thread
                    var childTask = activity.Execute();
                    childTask.Wait();
                });

                state.Attach(task);
                return state;
            }
        }
    }
}