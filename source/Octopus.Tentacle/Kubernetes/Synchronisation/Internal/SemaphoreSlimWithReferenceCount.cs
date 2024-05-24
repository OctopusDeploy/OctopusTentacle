using System;
using System.Threading;

namespace Octopus.Tentacle.Kubernetes.Synchronisation.Internal
{
    class SemaphoreSlimWithReferenceCount : SemaphoreSlim
    {
        int referenceCount;

        public SemaphoreSlimWithReferenceCount(int initialCount, int maxCount): base(initialCount, maxCount)
        {
            referenceCount = 0;
        }
        
        public void IncrementReferenceCount()
        {
            Interlocked.Increment(ref referenceCount);
        }
        
        public void DecrementReferenceCount()
        {
            Interlocked.Decrement(ref referenceCount);
        }
        
        /// /// <summary>
        /// Used for representing the number of threads holding a reference to this semaphore.
        /// This information is useful for knowing when it is safe to remove the lock from a keyed lookup.
        /// </summary>
        /// <remarks>
        /// The reference count does not necessarily represent the number of threads waiting on the semaphore.
        /// </remarks>
        public int ReferenceCount => referenceCount;
    }
}