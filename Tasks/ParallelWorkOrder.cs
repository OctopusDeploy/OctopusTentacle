using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Tasks
{
    public class ParallelWorkOrder<T>
    {
        readonly int maxParallelism;
        readonly Action<T> executeCallback;
        readonly ITaskContext taskContext;
        readonly List<WorkItem<T>> completed = new List<WorkItem<T>>();
        readonly List<WorkItem<T>> running = new List<WorkItem<T>>();
        readonly Queue<Planned<T>> pending = new Queue<Planned<T>>();

        public ParallelWorkOrder(IEnumerable<Planned<T>> work, int maxParallelism, Action<T> executeCallback, ITaskContext taskContext)
        {
            this.maxParallelism = maxParallelism;
            this.executeCallback = executeCallback;
            this.taskContext = taskContext;
            foreach (var item in work) pending.Enqueue(item);
        }

        public void Execute()
        {
            SchedulingLoop();

            foreach (var wi in running)
                wi.WaitForCompletion();

            taskContext.EnsureNotCanceled();

            AssertNoErrorsOccured();
        }


        void SchedulingLoop()
        {
            while (pending.Count > 0 || running.Count > 0)
            {
                CheckForCompletedTasks();
                if (taskContext.IsCancellationRequested)
                    return;

                if (GetExceptionsThusFar().Any())
                    return;

                ScheduleNextWork();
                Thread.Sleep(100);
            }
        }

        void ScheduleNextWork()
        {
            for (var i = 0; i < (maxParallelism - running.Count) && i < pending.Count; i++)
            {
                var currentThreadName = Thread.CurrentThread.Name;

                var item = pending.Dequeue();
                var closure = new OctoThreadClosure<T>(item, executeCallback);
                var thread = new Thread(closure.Execute);
                thread.Name = currentThreadName + " -> " + item.Name;
                running.Add(new WorkItem<T>(thread, closure));
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.BelowNormal;
                thread.Start();
            }
        }

        void CheckForCompletedTasks()
        {
            for (var i = 0; i < running.Count; i++)
            {
                var item = running[i];
                if (!item.HasCompleted())
                    continue;

                running.Remove(item);
                completed.Add(item);
                i--;
            }
        }

        List<Exception> GetExceptionsThusFar()
        {
            return (from pair in completed where pair.Exception != null select pair.Exception).ToList();
        }

        void AssertNoErrorsOccured()
        {
            var errors = GetExceptionsThusFar();
            if (errors.Count == 0)
                return;

            if (errors.Any(e => e is OperationCanceledException))
            {
                throw new TaskCanceledException("One or more child activities were canceled.");
            }

            throw new ActivityFailedException(BuildMessage(errors), new AggregateException(errors));
        }

        string BuildMessage(List<Exception> errors)
        {
            if (errors.Count == 1)
            {
                return errors.Single() is ActionFailedException
                    ? $"Activity {errors.Single().Message} failed with error '{errors.Single().InnerException.Message}'."
                    : $"Activity failed with error '{errors.Single().Message}'.";
            }

            return $"Activities failed with errors {string.Join(", ", errors.Select(GetMessage))}";
        }

        string GetMessage(Exception ex)
        {
            return ex is ActionFailedException
                ? $"{ex.Message}: '{ex.InnerException.Message}'"
                : $"'{ex.Message}'";
        }
    }
}