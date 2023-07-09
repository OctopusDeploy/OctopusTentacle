using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Retries
{
    internal static class CancellationTokenExtensionMethods
    {
        public static Task<TResult> AsTask<TResult>(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            IDisposable? registration = null;
            registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
                registration?.Dispose();
            }, useSynchronizationContext: false);

            return tcs.Task;
        }

        public static Task AsTask(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<VoidResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            IDisposable? registration = null;
            registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
                registration?.Dispose();
            }, useSynchronizationContext: false);

            return tcs.Task;
        }

        private struct VoidResult { }
    }
}