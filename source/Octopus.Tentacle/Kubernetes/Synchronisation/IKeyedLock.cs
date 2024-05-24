using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes.Synchronisation
{
    public interface IKeyedLock<in TKey>
    {
        Task<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken);
    }
}