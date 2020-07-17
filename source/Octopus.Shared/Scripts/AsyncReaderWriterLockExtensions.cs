using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Octopus.Shared.Scripts
{
    public static class AsyncReaderWriterLockExtensions
    {
        public static bool TryEnterReadLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable? releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                try
                {
                    releaseLock = @lock.ReaderLock(linkedCancellationTokenSource.Token);
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }

        public static bool TryEnterWriteLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, CancellationToken cancellationToken, out IDisposable? releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            {
                try
                {
                    releaseLock = @lock.WriterLock(linkedCancellationTokenSource.Token);
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