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
    }
}
