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
        readonly ILog log;

        private ActivityRuntime(ActivityState parentState, CancellationTokenSource cancellation, ILog log)
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
            var log = new StringBuilder();
            using (LogTapper.CaptureTo(log))
            {
                var state = ConfigureChildActivity(activity, log);
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
        }

        void HandleError(Exception exception)
        {
            if (exception is TaskCanceledException)
            {
                log.Error("The task was cancelled.");
            }
            if (exception is ActivityFailedException)
            {
                log.Error(exception.Message);
            }
            else if (exception is AggregateException)
            {
                foreach (var item in ((AggregateException)exception).InnerExceptions)
                {
                    HandleError(item);
                }
            }
            else
            {
                log.Error(exception);
            }
        }

        ActivityState ConfigureChildActivity(object activity, StringBuilder logOutput)
        {
            var name = activity.ToString();
            var named = activity as IHaveName;
            if (named != null)
            {
                name = named.Name;
            }

            var childState = new ActivityState(name, logOutput);
            var runtimeAware = activity as IRuntimeAware;
            if (runtimeAware != null)
            {
                runtimeAware.Runtime = new ActivityRuntime(childState, cancellation, log);
            }

            if (parentState != null)
            {
                parentState.AddChild(childState);
            }

            return childState;
        }

        public static IActivityState BeginExecute(IActivity activity, ILog log)
        {
            return BeginExecute(activity, null, log);
        }

        public static IActivityState BeginExecute(IActivity activity, CancellationTokenSource cancellation, ILog log)
        {
            var runtime = new ActivityRuntime(null, cancellation ?? new CancellationTokenSource(), log);
            var logOutput = new StringBuilder();
            
            using (LogTapper.CaptureTo(logOutput))
            {
                var state = runtime.ConfigureChildActivity(activity, logOutput);
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