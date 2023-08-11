﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    internal static class CancellationTokenExtensionMethods
    {
        public static Task<TResult> AsTask<TResult>(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TResult>();

            IDisposable registration = null;
            registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
                registration?.Dispose();
            }, useSynchronizationContext: false);

            return tcs.Task;
        }

        public static Task AsTask(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<VoidResult>();

            IDisposable registration = null;
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
