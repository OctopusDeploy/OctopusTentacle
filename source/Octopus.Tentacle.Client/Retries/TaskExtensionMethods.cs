using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Retries
{
    static class TaskExtensionMethods
    {
        public static void IgnoreUnobservedExceptions(this Task task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                {
                    var observedException = task.Exception;
                }

                return;
            }

            task.ContinueWith(
                t =>
                {
                    var observedException = t.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public static async Task<TaskCompletionResult> WaitTillCompletion(this Task actionTask, TimeSpan abandonAfter, CancellationToken cancellationToken)
        {
            var actionTaskCompleted = await actionTask.WaitTillCompletedOrCancelled(cancellationToken);

            if (actionTaskCompleted) return TaskCompletionResult.Completed;

            actionTaskCompleted = await actionTask.WaitTillCompletedOrTimeout(abandonAfter);

            if (actionTaskCompleted) return TaskCompletionResult.Completed;

            actionTask.IgnoreUnobservedExceptions();
            return TaskCompletionResult.Abandoned;
        }

        static async Task<bool> WaitTillCompletedOrCancelled(this Task taskToWaitFor, CancellationToken cancellationToken)
        {
            using var cleanupCancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cleanupCancellationTokenSource.Token);

            var completedTask = await Task.WhenAny(taskToWaitFor, Task.Delay(-1, linkedCancellationTokenSource.Token));

            cleanupCancellationTokenSource.Cancel();

            return taskToWaitFor == completedTask;
        }

        static async Task<bool> WaitTillCompletedOrTimeout(this Task taskToWaitFor, TimeSpan delay)
        {
            using var cleanupCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(taskToWaitFor, Task.Delay(delay, cleanupCancellationTokenSource.Token));

            cleanupCancellationTokenSource.Cancel();

            return taskToWaitFor == completedTask;
        }
    }
}
