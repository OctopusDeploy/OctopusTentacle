using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Retries
{
    internal interface IRpcCallRetryHandler
    {
        TimeSpan RetryTimeout { get; }
        
        Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action,
            RpcCallRetryHandler.OnRetyAction? onRetryAction,
            RpcCallRetryHandler.OnTimeoutAction? onTimeoutAction,
            bool abandonActionOnCancellation,
            TimeSpan abandonAfter,
            CancellationToken cancellationToken);
    }
}