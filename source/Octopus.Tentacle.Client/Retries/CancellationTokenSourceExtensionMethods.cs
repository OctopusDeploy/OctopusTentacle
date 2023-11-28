using System;
using System.Threading;

namespace Octopus.Tentacle.Client.Retries
{
    internal static class CancellationTokenSourceExtensionMethods
    {
        public static void TryCancel(this CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (Exception) { }
        }

        public static void TryCancelAfter(this CancellationTokenSource cancellationTokenSource, TimeSpan deplay)
        {
            try
            {
                cancellationTokenSource?.CancelAfter(deplay);
            }
            catch (Exception) { }
        }
    }
}