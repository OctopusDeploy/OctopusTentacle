using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tasks
{
    public class RunningTask : ITaskContext, IRunningTask
    {
        readonly string description;
        readonly string additionalDescription;
        readonly Type rootTaskControllerType;
        readonly object arguments;
        readonly ILifetimeScope lifetimeScope;
        readonly TaskCompletionHandler completeCallback;
        readonly ManualResetEventSlim complete = new ManualResetEventSlim(false);
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly Thread workThread;
        readonly ILogWithContext log = Log.Octopus();
        readonly LogContext taskLogContext;
        bool isPaused;
        bool failedToComplete;
        Exception finalException;

        public RunningTask(string taskId, string logCorrelationId, string description, string additionalDescription, Type rootTaskControllerType, object arguments, ILifetimeScope lifetimeScope, TaskCompletionHandler completeCallback)
        {
            this.Id = taskId;
            this.description = description;
            this.additionalDescription = additionalDescription;
            this.rootTaskControllerType = rootTaskControllerType;
            this.arguments = arguments;
            this.lifetimeScope = lifetimeScope;
            this.completeCallback = completeCallback;

            taskLogContext = LogContext.CreateNew(logCorrelationId);
            workThread = new Thread(RunMainThread) { Name = taskId + ": " + description };
        }

        public string Id { get; }

        public string TaskId => Id;

        public bool IsCancellationRequested => cancel.IsCancellationRequested;

        public CancellationToken CancellationToken => cancel.Token;

        public void Start()
        {
            workThread.Start();
        }

        void RunMainThread()
        {
            using (log.WithinBlock(taskLogContext))
            {
                var fullTaskDescription = description;
                if (!string.IsNullOrEmpty(additionalDescription))
                    fullTaskDescription = $"{fullTaskDescription} {additionalDescription}";
                log.Info(fullTaskDescription);

                using (var workScope = lifetimeScope.BeginLifetimeScope())
                {
                    try
                    {
                        var builder = new ContainerBuilder();
                        builder.RegisterInstance<ITaskContext>(this);
                        builder.RegisterInstance(arguments).AsSelf().AsImplementedInterfaces();
                        builder.Update(workScope.ComponentRegistry);

                        var controller = (ITaskController)workScope.Resolve(rootTaskControllerType);
                        workThread.Priority = controller.ExecutionPriority;
                        controller.Execute();
                    }
                    catch (Exception e)
                    {
                        finalException = e;
                        var root = e.UnpackFromContainers();

                        if (root is OperationCanceledException || root is ThreadAbortException)
                        {
                            // These happen as part of cancellation. It's enough to just return them, without logging them.
                        }
                        else
                        {
                            log.Fatal(root.PrettyPrint(false));
                        }
                    }
                    finally
                    {
                        CompleteTask();
                    }
                }
              
            }
        }

        void FinishLog()
        {
            if (!IsPaused() || IsCancellationRequested)
            {
                log.Finish();
            }
        }

        public void Cancel()
        {
            using (log.WithinBlock(taskLogContext))
            {
                if (IsCancellationRequested)
                {
                    return;
                }

                log.Info("Requesting cancellation...");
                cancel.Cancel();
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

        public bool FailedToComplete()
        {
            return failedToComplete;
        }

        public void SleepUnlessCancelled(TimeSpan duration)
        {
            CancellationToken.WaitHandle.WaitOne(duration);
        }

        public void EnsureNotCanceled()
        {
            if (IsCancellationRequested)
            {
                throw new TaskCanceledException("This task has been canceled.");
            }
        }

        void CompleteTask()
        {
            try
            {
                complete.Set();

                completeCallback?.Invoke(Id, finalException);
                FinishLog();
            }
            catch (Exception completeEx)
            {
                failedToComplete = true;
                log.Warn("Unable to mark task as complete, will continue to retry");
                log.Warn(completeEx.PrettyPrint(false));
            }
        }

        public void ReattemptCompleteTask()
        {
            completeCallback?.Invoke(Id, finalException);
            failedToComplete = false;
            using (log.WithinBlock(taskLogContext))
            {
                log.Info("Sucessfully marked task as complete");
                FinishLog();
            }
        }

    }
}