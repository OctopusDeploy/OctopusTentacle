using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Octopus.Shared.Scripts
{
    public static class AsyncReaderWriterLockExtensions
    {
        class DebugDisposable : IDisposable
        {
            readonly IDisposable resource;
            readonly Action<string> log;
            readonly string lockType;

            public DebugDisposable(IDisposable resource, Action<string> log, string lockType)
            {
                this.resource = resource;
                this.log = log;
                this.lockType = lockType;
            }

            public void Dispose()
            {
                log($"Releasing {lockType}.");
                resource.Dispose();
            }
        }

        public static bool TryEnterReadLock(this AsyncReaderWriterLock @lock, Action<string> log, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                try
                {
                    releaseLock = new DebugDisposable(@lock.ReaderLock(linkedCancellationTokenSource.Token), log, "read lock");
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }

        public static bool TryEnterWriterLock(this AsyncReaderWriterLock @lock, Action<string> log, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                try
                {
                    releaseLock = new DebugDisposable(@lock.WriterLock(linkedCancellationTokenSource.Token), log, "write lock");
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }
    }
}