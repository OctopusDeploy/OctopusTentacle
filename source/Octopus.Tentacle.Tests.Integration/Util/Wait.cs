using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public static class Wait
    {
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
    }
}