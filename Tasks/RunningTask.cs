using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Halibut;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Logging;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tasks
{
    public class RunningTask : ITaskContext, IRunningTask
    {
        public const string TaskCancellationTimeoutName = "TaskCancellationTimeout";
        static readonly TimeSpan DefaultCancellationTime = TimeSpan.FromSeconds(30);

        readonly string taskId;
        readonly string description;
        readonly Type rootTaskControllerType;
        readonly object arguments;
        readonly ILifetimeScope lifetimeScope;
        readonly TaskCompletionHandler completeCallback;
        readonly ManualResetEventSlim complete = new ManualResetEventSlim(false);
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly Thread workThread;
        readonly ILog log = Log.Octopus();
        readonly LogCorrelator taskLogCorrelator;
        readonly TimeSpan taskCancellationTimeout;
        bool isPaused;

        public RunningTask(string taskId, string description, Type rootTaskControllerType, object arguments, ILifetimeScope lifetimeScope, TaskCompletionHandler completeCallback)
        {
            this.taskId = taskId;
            this.description = description;
            this.rootTaskControllerType = rootTaskControllerType;
            this.arguments = arguments;
            this.lifetimeScope = lifetimeScope;
            this.completeCallback = completeCallback;

            try
            {
                taskCancellationTimeout = lifetimeScope.ResolveNamed<TimeSpan>(TaskCancellationTimeoutName);
            }
            catch (Exception)
            {
                taskCancellationTimeout = DefaultCancellationTime;
            }
            
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
                log.Info(description);

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
                        ex = e;
                        var root = e.UnpackFromContainers();

                        if (root is ActivityFailedException)
                        {
                            log.Error(root.Message);
                        }
                        else if (root is HalibutClientException)
                        {
                            log.Error(root.Message);
                        }
                        else if (root is TaskCanceledException || root is ThreadAbortException)
                        {
                            // These happen as part of cancellation. It's enough to just return them, without logging them.
                        }
                        else
                        {
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

                var finished = workThread.Join(taskCancellationTimeout);
                if (finished)
                {
                    return;
                }

                log.Warn("Cancellation did not complete within a reasonable time. Aborting the thread.");
                workThread.Abort();

                CompleteTask(null);
            }
        }

        public void Pause()
        {
            isPaused = true;
        }

        public bool IsPaused()
        {
            return isPaused;
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
            if (error == null)
            {
                log.Finish();
            }

            complete.Set();

            if (completeCallback != null)
            {
                completeCallback(taskId, error);
            }
        }
    }
}