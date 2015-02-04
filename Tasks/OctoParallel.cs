using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tasks
{
    public class OctoParallel
    {
        static readonly ILog Log = Diagnostics.Log.Octopus();

        public static void ForEach<T>(IEnumerable<Planned<T>> workItems, Action<T> executeCallback)
        {
            var items = workItems.ToList();

            var threads = SpawnThreads(executeCallback, items);

            WaitForThreadsToComplete(threads);

            ThrowIfChildrenFailed(threads);
        }

        static List<Tuple<Thread, OctoThreadClosure<T>>> SpawnThreads<T>(Action<T> executeCallback, List<Planned<T>> items)
        {
            var currentThreadName = Thread.CurrentThread.Name;
            var threads = new List<Tuple<Thread, OctoThreadClosure<T>>>(items.Count);

            foreach (var item in items)
            {
                var closure = new OctoThreadClosure<T>(item, executeCallback);
                var thread = new Thread(closure.Execute);
                thread.Name = currentThreadName + " -> " + item.Name;
                threads.Add(Tuple.Create(thread, closure));
                thread.Start();
            }

            return threads;
        }

        static void WaitForThreadsToComplete<T>(IEnumerable<Tuple<Thread, OctoThreadClosure<T>>> threads)
        {
            foreach (var pair in threads)
            {
                pair.Item1.Join();
            }
        }

        static void ThrowIfChildrenFailed<T>(IEnumerable<Tuple<Thread, OctoThreadClosure<T>>> threads)
        {
            var errors = (from pair in threads where pair.Item2.Exception != null select pair.Item2.Exception).ToList();

            if (errors.Count > 0)
            {
                if (errors.All(e => e is TaskCanceledException))
                {
                    throw new TaskCanceledException("One or more child activities were canceled.");
                }

                throw new ActivityFailedException("One or more child activities failed.");
            }
        }

        class OctoThreadClosure<T> 
        {
            readonly Planned<T> item;
            readonly Action<T> executeCallback;

            public OctoThreadClosure(Planned<T> item, Action<T> executeCallback)
            {
                this.item = item;
                this.executeCallback = executeCallback;
            }

            public Exception Exception { get; private set; }

            public void Execute()
            {
                using (Log.WithinBlock(item.LogCorrelator))
                {
                    try
                    {
                        executeCallback(item.WorkItem);
                        Log.Finish();
                    }
                    catch (Exception ex)
                    {
                        if (ex is ActivityFailedException)
                        {
                            Log.Error(ex.Message);
                        }
                        else if (ex is HalibutClientException)
                        {
                            Log.Error(ex.Message);
                        }
                        else if (ex is TaskCanceledException)
                        {
                            Log.Info(ex.Message);
                        }
                        else
                        {
                            Log.Error(ex);
                        }

                        Exception = ex;
                    }
                }
            }
        }
    }
}