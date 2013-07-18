using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Util;

namespace Octopus.Shared.Activities
{
    public class ActivityRuntime : IActivityRuntime
    {
        private readonly IActivityMessage message;
        readonly ActivityState state;
        readonly CancellationTokenSource cancellation;
        readonly IActivityLog log;
        private readonly IActivityResolver activityResolver;
        private readonly TaskFactory taskFactory;

        public ActivityRuntime(IActivityMessage message, ActivityState state, CancellationTokenSource cancellation, IActivityLog log, IActivityResolver activityResolver, TaskFactory taskFactory)
        {
            this.message = message;
            this.cancellation = cancellation;
            this.log = log;
            this.activityResolver = activityResolver;
            this.taskFactory = taskFactory;
            this.state = ConfigureChildActivity(message, state);
        }

        public ActivityState State
        {
            get { return state; }
        }

        public bool IsCancellationRequested { get { return cancellation.IsCancellationRequested; } }

        public TaskFactory TaskFactory { get { return taskFactory; } }

        public void Start()
        {
            dynamic activity = activityResolver.Locate(message);
            activity.Runtime = this;
            activity.Log = state.Log;

            var task = (Task)activity.Execute((dynamic)message);
            state.Attach(task);
        }

        public async Task Execute()
        {
            dynamic activity = activityResolver.Locate(message);
            activity.Runtime = this;
            activity.Log = state.Log;

            var task = (Task)activity.Execute((dynamic)message);
            state.Attach(task);

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                HandleError(state, ex);
                throw;
            }
        }
        
        public void EnsureNotCancelled()
        {
            if (IsCancellationRequested)
            {
                if (log != null)
                    log.Error("The activity was canceled.");
                throw new TaskCanceledException("The activity was canceled.");
            }
        }

        public CancellationToken CancellationToken { get { return cancellation.Token; } }

        public Task Execute(IActivityMessage activity)
        {
            return ExecuteChild(activity, taskFactory);
        }

        public Task Execute(IEnumerable<IActivityMessage> activities)
        {
            var tasks = activities.Select(Execute).ToList();
            return TaskEx.WhenAll(tasks);
        }

        public Task Execute(IEnumerable<IActivityMessage> activities, int maxParallelismForChildTasks)
        {
            var factory = maxParallelismForChildTasks >= 1 
                ? new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(maxParallelismForChildTasks)) 
                : taskFactory;

            var tasks = activities.Select(a => ExecuteChild(a, factory)).ToList();
            return TaskEx.WhenAll(tasks);
        }

        Task ExecuteChild(IActivityMessage activity, TaskFactory customFactory)
        {
            var runtime = new ActivityRuntime(activity, state, cancellation, new ActivityLog(), activityResolver, customFactory);
            return runtime.Execute();
        }

        void HandleError(ActivityState childState, Exception exception)
        {
            exception = exception.GetRootError();
            if (exception is TaskCanceledException)
            {
                childState.Log.Error("The task was canceled.");
            }
            else if (exception is ActivityFailedException)
            {
                childState.Log.Error(exception.Message);
            }
            else
            {
                childState.Log.Error(exception);
            }
        }

        public ActivityState ConfigureChildActivity(IActivityMessage activity, ActivityState parent)
        {
            var name = new Func<string>(() => string.IsNullOrWhiteSpace(activity.Name) ? activity.GetType().DeclaringType == null ? activity.GetType().Name : activity.GetType().DeclaringType.Name : activity.Name);
            var tag = activity.Tag;

            var childState = new ActivityState(name, tag, Guid.NewGuid().ToString(), cancellation);

            if (parent != null)
            {
                parent.AddChild(childState);
            }

            return childState;
        }
    }
}