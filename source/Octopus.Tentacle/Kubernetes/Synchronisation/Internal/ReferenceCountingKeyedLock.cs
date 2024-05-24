using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    class ReferenceCountingKeyedLock<TKey> : IKeyedLock<TKey> where TKey : IEquatable<TKey>
    {
        readonly ISemaphoreSlimReleaserFactory<SemaphoreSlimWithReferenceCount> releaserFactory;
        readonly Dictionary<TKey, ISemaphoreSlimReleaser<SemaphoreSlimWithReferenceCount>> keyedLocks = new();

        // This lock ensures atomicity for reference count increment/decrement and dictionary access to prevent race conditions.
        readonly object @lock = new();

        public ReferenceCountingKeyedLock(ISemaphoreSlimReleaserFactory<SemaphoreSlimWithReferenceCount> releaserFactory)
        {
            this.releaserFactory = releaserFactory;
        }

        public async Task<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken)
        {
            ISemaphoreSlimReleaser<SemaphoreSlimWithReferenceCount>? keyedLock;
            lock (@lock)
            {
                if (!keyedLocks.TryGetValue(key, out keyedLock))
                {
                    var referenceCountingSemaphore = new SemaphoreSlimWithReferenceCount(1, 1);
                    keyedLock = releaserFactory.Create(referenceCountingSemaphore, () => TryRemoveLock(key, referenceCountingSemaphore));
                    keyedLocks[key] = keyedLock;
                }
                keyedLock.Semaphore.IncrementReferenceCount();
            }

            await keyedLock.Semaphore.WaitAsync(cancellationToken);
            return keyedLock;
        }

        void TryRemoveLock(TKey key, SemaphoreSlimWithReferenceCount semaphoreSlimWithReferenceCount)
        {
            /* There must not be any room for concurrent dictionary access between reference count decrement and dictionary key removal.
             * Otherwise it is possible for a second thread to get a key right before it is removed from the dictionary,
             * which would open up the possibility for a third thread to create a new lock for the same key.
             */
            lock (@lock)
            {
                semaphoreSlimWithReferenceCount.DecrementReferenceCount();
                if (semaphoreSlimWithReferenceCount.ReferenceCount == 0)
                {
                    keyedLocks.Remove(key);
                    semaphoreSlimWithReferenceCount.Dispose();
                }
            }
        }
    }
}