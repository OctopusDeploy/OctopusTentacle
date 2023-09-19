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

        public static async Task<bool> WaitTillCompletedOrAbandoned(this Task actionTask, TimeSpan abandonAfter, CancellationToken cancellationToken)
        {
            var actionTaskCompleted = await actionTask.WaitTillCompletedOrCancelled(cancellationToken);

            if (!actionTaskCompleted)
            {
                actionTaskCompleted = await actionTask.WaitTillCompletedOrTimeout(abandonAfter).ConfigureAwait(false);

                if (!actionTaskCompleted)
                {
                    actionTask.IgnoreUnobservedExceptions();
                    return false;
                }
            }

            return true;
        }

        public static async Task<bool> WaitTillCompletedOrCancelled(this Task taskToWaitFor, CancellationToken cancellationToken)
        {
            using var cleanupCancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cleanupCancellationTokenSource.Token);

            var completedTask = await Task.WhenAny(taskToWaitFor, Task.Delay(-1, linkedCancellationTokenSource.Token)).ConfigureAwait(false);

            cleanupCancellationTokenSource.Cancel();

            return taskToWaitFor == completedTask;
        }

        public static async Task<bool> WaitTillCompletedOrTimeout(this Task taskToWaitFor, TimeSpan delay)
        {
            using var cleanupCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(taskToWaitFor, Task.Delay(delay, cleanupCancellationTokenSource.Token)).ConfigureAwait(false);

            cleanupCancellationTokenSource.Cancel();

            return taskToWaitFor == completedTask;
        }
    }
}
