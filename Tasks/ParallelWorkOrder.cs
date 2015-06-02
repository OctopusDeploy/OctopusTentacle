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
        readonly CancellationToken cancellation;
        readonly List<WorkItem<T>> completed = new List<WorkItem<T>>();
        readonly List<WorkItem<T>> running = new List<WorkItem<T>>();
        readonly Queue<Planned<T>> pending = new Queue<Planned<T>>();

        public ParallelWorkOrder(IEnumerable<Planned<T>> work, int maxParallelism, Action<T> executeCallback, CancellationToken cancellation)
        {
            this.maxParallelism = maxParallelism;
            this.executeCallback = executeCallback;
            this.cancellation = cancellation;
            foreach (var item in work) pending.Enqueue(item);
        }

        public void Execute()
        {
            while (pending.Count > 0 || running.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                CheckForCompletedTasks();
                AssertAllCompletedTasksThusFarAreSuccessful();
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

        void AssertAllCompletedTasksThusFarAreSuccessful()
        {
            var errors = (from pair in completed where pair.Exception != null select pair.Exception).ToList();

            if (errors.Count <= 0)
                return;

            if (errors.Any(e => e is TaskCanceledException || e is OperationCanceledException))
            {
                throw new TaskCanceledException("One or more child activities were canceled.");
            }

            throw new ActivityFailedException("One or more child activities failed.");
        }
    }
}