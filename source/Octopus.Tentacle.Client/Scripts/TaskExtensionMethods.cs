using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Scripts
{
    internal static class TaskExtensionMethods
    {
        public static async Task SuppressOperationCanceledException(this Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException) { }
        }
    }
}
