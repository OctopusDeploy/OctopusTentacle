using System;
using System.Threading.Tasks;
using Polly;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RetryHelper
    {
        public static async Task<TResult> RetryAsync<TResult, TException>(Func<Task<TResult>> command, int retryCount = 10, int retryBackoffDurationMilliseconds = 100)
            where TException : Exception
        {
            return await Policy<TResult>
                .Handle<TException>()
                .WaitAndRetryAsync(retryCount, rc => TimeSpan.FromMilliseconds(retryBackoffDurationMilliseconds * rc))
                .ExecuteAsync(command);
        }
        
        public static async Task<TResult> RetryAsync<TResult, TException>(
            Func<Task<TResult>> command, ILogger logger, int retryCount = 10, int retryBackoffDurationMilliseconds = 100)
            where TException : Exception
        {
            return await Policy<TResult>
                .Handle<TException>()
                .WaitAndRetryAsync(retryCount, rc =>
                {
                    logger.Information("Retrying command at {Time}", DateTimeOffset.UtcNow);
                    return TimeSpan.FromMilliseconds(retryBackoffDurationMilliseconds * rc);
                })
                .ExecuteAsync(command);
        }
    }
}
