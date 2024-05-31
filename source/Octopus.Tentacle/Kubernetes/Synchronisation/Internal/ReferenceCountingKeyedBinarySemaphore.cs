using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
 
    class ReferenceCountingKeyedBinarySemaphore<TKey> : IKeyedSemaphore<TKey> where TKey : IEquatable<TKey>
    {
        readonly Dictionary<TKey, SemaphoreSlimReleaser<ReferenceCountingBinarySemaphoreSlim>> keyedLocks = new();


        public async Task<IDisposable> WaitAsync(TKey key, CancellationToken cancellationToken)
        {
            SemaphoreSlimReleaser<ReferenceCountingBinarySemaphoreSlim>? keyedLock;
            lock (keyedLocks)
            {
                if (!keyedLocks.TryGetValue(key, out keyedLock))
                {
                    var referenceCountingSemaphore = new ReferenceCountingBinarySemaphoreSlim();
                    keyedLock = new SemaphoreSlimReleaser<ReferenceCountingBinarySemaphoreSlim>(referenceCountingSemaphore, () => TryRemoveLock(key, referenceCountingSemaphore));
                    keyedLocks[key] = keyedLock;
                }
                else
                {
                    keyedLock.Semaphore.IncrementReferenceCount();
                }
            }

            await keyedLock.Semaphore.WaitAsync(cancellationToken);
            return keyedLock;
        }

        void TryRemoveLock(TKey key, ReferenceCountingBinarySemaphoreSlim referenceCountingBinarySemaphoreSlim)
        {
            /* There must not be any room for concurrent dictionary access between reference count decrement and dictionary key removal.
             * Otherwise it is possible for a second thread to get a key right before it is removed from the dictionary,
             * which would open up the possibility for a third thread to create a new lock for the same key.
             */
            lock (keyedLocks)
            {
                referenceCountingBinarySemaphoreSlim.DecrementReferenceCount();
                if (referenceCountingBinarySemaphoreSlim.ReferenceCount == 0)
                {
                    keyedLocks.Remove(key);
                    referenceCountingBinarySemaphoreSlim.Dispose();
                }
            }
        }
    }
}