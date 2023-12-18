using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public static class Wait
    {
        public static async Task For(Func<bool> toBeTrue, TimeSpan timeout, Action onTimeoutOrCancellation, CancellationToken cancellationToken)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

            try
            {
                await For(toBeTrue, linkedCancellationTokenSource.Token);
            }
            catch (OperationCanceledException) when (linkedCancellationTokenSource.IsCancellationRequested)
            {
                onTimeoutOrCancellation();
            }
        }

        public static async Task For(Func<bool> toBeTrue, CancellationToken cancellationToken)
        {
            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                if(toBeTrue()) return;
                stopWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(stopWatch.Elapsed + TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }

        public static async Task For(Func<Task<bool>> toBeTrue, CancellationToken cancellationToken)
        {
            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                if (await toBeTrue()) return;
                stopWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(stopWatch.Elapsed + TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }
    }
}