using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RetryHelper
    {
        public static async Task<TResult> RetryAsync<TResult, TException>(Func<CancellationToken, Task<TResult>> command, CancellationToken cancellationToken, int retryCount = 10, int retryBackoffDurationMilliseconds = 100)
            where TException : Exception
        {
            return await Policy<TResult>
                .Handle<TException>()
                .WaitAndRetryAsync(retryCount, rc => TimeSpan.FromMilliseconds(retryBackoffDurationMilliseconds * rc))
                .ExecuteAsync(command, cancellationToken);
        }
    }
}
