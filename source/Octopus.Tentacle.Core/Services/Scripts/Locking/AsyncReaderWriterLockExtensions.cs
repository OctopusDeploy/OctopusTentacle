using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Octopus.Tentacle.Scripts
{
    public static class AsyncReaderWriterLockExtensions
    {
        public static AcquireLockResult TryEnterReadLock(this AsyncReaderWriterLock @lock, CancellationToken cancellationToken)
        {
            try
            {
                return AcquireLockResult.Success(@lock.ReaderLock(cancellationToken));
            }
            catch (TaskCanceledException)
            {
                return AcquireLockResult.Fail();
            }
        }

        public static AcquireLockResult TryEnterWriteLock(this AsyncReaderWriterLock @lock, CancellationToken cancellationToken)
        {
            try
            {
                return AcquireLockResult.Success(@lock.WriterLock(cancellationToken));
            }
            catch (TaskCanceledException)
            {
                return AcquireLockResult.Fail();
            }
        }
    }
}