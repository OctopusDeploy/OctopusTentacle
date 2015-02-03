using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tasks
{
    public class RunningTask : ITaskContext, IRunningTask
    {
        readonly string taskId;
        readonly Type rootTaskControllerType;
        readonly object arguments;
        readonly ILifetimeScope lifetimeScope;
        readonly TaskCompletionHandler completeCallback;
        readonly ManualResetEventSlim complete = new ManualResetEventSlim(false);
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly Thread workThread;
        readonly ILog log = Log.Octopus();
        readonly LogCorrelator taskLogCorrelator;

        public RunningTask(string taskId, string description, Type rootTaskControllerType, object arguments, ILifetimeScope lifetimeScope, TaskCompletionHandler completeCallback)
        {
            this.taskId = taskId;
            this.rootTaskControllerType = rootTaskControllerType;
            this.arguments = arguments;
            this.lifetimeScope = lifetimeScope;
            this.completeCallback = completeCallback;

            taskLogCorrelator = LogCorrelator.CreateNew(taskId);
            workThread = new Thread(RunMainThread) {Name = taskId + ": " + description};
        }

        public void Start()
        {
            workThread.Start();
        }

        void RunMainThread()
        {
            using (log.WithinBlock(taskLogCorrelator))
            {
                using (var workScope = lifetimeScope.BeginLifetimeScope())
                {
                    Exception ex = null;
                    try
                    {
                        var builder = new ContainerBuilder();
                        builder.RegisterInstance<ITaskContext>(this);
                        builder.RegisterInstance(arguments).AsSelf().AsImplementedInterfaces();
                        builder.Update(workScope.ComponentRegistry);

                        var controller = (ITaskController) workScope.Resolve(rootTaskControllerType);

                        controller.Execute();
                    }
                    catch (Exception e)
                    {
                        var root = e.UnpackFromContainers();
                        if (!(root is TaskCanceledException || root is ThreadAbortException))
                        {
                            ex = e;
                            log.Error(ex);
                        }
                    }
                    finally
                    {
                        CompleteTask(ex);
                    }
                }
            }
        }

        public void Cancel()
        {
            using (log.WithinBlock(taskLogCorrelator))
            {
                if (cancel.IsCancellationRequested)
                {
                    return;
                }

                log.Info("Requesting cancellation...");
                cancel.Cancel();

                var finished = workThread.Join(TimeSpan.FromSeconds(30));
                if (finished)
                {
                    return;
                }

                log.Warn("Cancellation did not complete within a reasonable time. Aborting the thread.");
                workThread.Abort();

                CompleteTask(null);
            }
        }

        public bool IsCancellationRequested
        {
            get { return cancel.IsCancellationRequested; }
        }

        public CancellationToken CancellationToken
        {
            get { return cancel.Token; }
        }

        public void EnsureNotCancelled()
        {
            if (IsCancellationRequested)
            {
                throw new TaskCanceledException("This task has been cancelled.");
            }
        }

        void CompleteTask(Exception error)
        {
            log.Finish();

            complete.Set();

            if (completeCallback != null)
            {
                completeCallback(taskId, error);
            }
        }
    }
}