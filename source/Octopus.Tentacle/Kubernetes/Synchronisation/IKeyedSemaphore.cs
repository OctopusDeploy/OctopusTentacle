using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes.Synchronisation
{
    public interface IKeyedSemaphore<in TKey>
    {
        
        /// <summary>
        /// Asynchronously waits to enter the semaphore for the specified key. Used for ensuring exclusive access for the key across threads.
        /// </summary>
        /// <returns> An <c>IDisposable</c> object that releases the key once disposed. </returns>
        /// <example>
        /// <code>
        /// using (await keyedSemaphore.WaitAsync(command.ScriptTicket, cancellationToken)) {
        ///     // ... do work
        /// }
        /// </code>
        /// </example>
        Task<IDisposable> WaitAsync(TKey key, CancellationToken cancellationToken);
    }
}