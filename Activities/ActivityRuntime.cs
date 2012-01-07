using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;
using log4net;

namespace Octopus.Shared.Activities
{
    public class ActivityRuntime : IActivityRuntime
    {
        readonly ActivityHandle parentHandle;
        readonly CancellationTokenSource cancellation;
        readonly ILog log;

        public ActivityRuntime(ILog log)
            : this (null, new CancellationTokenSource(), log)
        {
        }

        private ActivityRuntime(ActivityHandle parentHandle, CancellationTokenSource cancellation, ILog log)
        {
            this.parentHandle = parentHandle;
            this.cancellation = cancellation;
            this.log = log;
        }

        public CancellationTokenSource Cancellation
        {
            get { return cancellation; }
        }

        public IActivityHandle Execute(IActivity activity)
        {
            var state = BeginExecute(activity);
            state.WaitForComplete();

            if (state.Error != null)
            {
                throw new AggregateException("A child activity failed.", state.Error);
            }

            return state;
        }

        public IActivityHandleBatch Execute(IEnumerable<IActivity> activities)
        {
            var batch = BeginExecute(activities);
            batch.WaitForCompletion();

            var errors = batch.Activities.Select(s => s.Error).Where(s => s != null).ToArray();
            if (errors.Length > 0)
            {
                throw new AggregateException("One or more child activities failed", errors);
            }
            
            return batch;
        }

        public IActivityHandleBatch BeginExecute(IEnumerable<IActivity> activitiesToRun)
        {
            var activities = activitiesToRun.ToList();
            if (activities.Count == 0)
                return null;

            var states = new IActivityHandle[activities.Count];
            for (var i = 0; i < activities.Count; i++)
            {
                states[i] = BeginExecute(activities[i]);
            }

            return new ActivityHandleBatch(states);
        }

        public IActivityHandle BeginExecute(IActivity activity)
        {
            var name = activity.ToString();
            var named = activity as IHaveName;
            if (named != null)
            {
                name = named.Name;
            }

            var childState = new ActivityHandle(name);
            
            var spawnable = activity as ISpawnChildActivities;
            if (spawnable != null)
            {
                spawnable.Runtime = new ActivityRuntime(childState, cancellation, log);
            }

            if (parentHandle != null)
            {
                parentHandle.AddChild(childState);
            }

            ThreadPool.QueueUserWorkItem(RunTaskOnBackgroundThread, Tuple.Create(childState, activity));

            return childState;
        }

        private void RunTaskOnBackgroundThread(object arguments)
        {
            var info = (Tuple<ActivityHandle, IActivity>) arguments;
            var state = info.Item1;
            var activity = info.Item2;

            using (LogTapper.CaptureTo(state.Log))
            {
                try
                {
                    state.ChangeStatus(ActivityStatus.Running);

                    activity.Execute();

                    state.ChangeStatus(ActivityStatus.Success);
                }
                catch (TaskCanceledException cancel)
                {
                    log.Error(cancel.Message);
                    state.ChangeStatus(ActivityStatus.Failed, cancel);
                }
                catch (ActivityFailedException failed)
                {
                    log.Error(failed.Message);
                    state.ChangeStatus(ActivityStatus.Failed, failed);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    state.ChangeStatus(ActivityStatus.Failed, ex);
                }
            }
        }
    }
}