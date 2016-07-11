using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Octopus.Shared.Scripts
{
    public static class AsyncReaderWriterLockExtensions
    {
        public static bool TryEnterReadLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            {
                try
                {
                    releaseLock = @lock.ReaderLock(timeoutSource.Token);
                    return true;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
        }

        public static bool TryEnterWriterLock(this AsyncReaderWriterLock @lock, TimeSpan timeout, out IDisposable releaseLock)
        {
            releaseLock = null;
            using (var timeoutSource = new CancellationTokenSource(timeout))
            {
                try
                {
                    releaseLock = @lock.WriterLock(timeoutSource.Token);
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