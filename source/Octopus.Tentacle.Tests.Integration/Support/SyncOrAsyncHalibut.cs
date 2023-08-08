using Halibut.Util;
using System.Threading.Tasks;
using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public enum SyncOrAsyncHalibut
    {
        Sync,
        Async
    }

    public static class SyncOrAsyncExtensionMethods
    {
        public static SyncOrAsyncWithoutResult WhenSync(this SyncOrAsyncHalibut syncOrAsyncHalibut, Action action)
        {
            if (syncOrAsyncHalibut == SyncOrAsyncHalibut.Sync)
            {
                action();
            }

            return new(syncOrAsyncHalibut);
        }

        public static async Task WhenAsync(this SyncOrAsyncWithoutResult syncOrAsyncWithoutResult, Func<Task> action)
        {
            if (syncOrAsyncWithoutResult.SyncOrAsyncHalibut == SyncOrAsyncHalibut.Async)
            {
                await action();
            }
        }

        public static void WhenAsync(this SyncOrAsyncWithoutResult syncOrAsyncWithoutResult, Action action)
        {
            if (syncOrAsyncWithoutResult.SyncOrAsyncHalibut == SyncOrAsyncHalibut.Async)
            {
                action();
            }
        }

        public static SyncOrAsyncWithResult<T> WhenSync<T>(this SyncOrAsyncHalibut syncOrAsyncHalibut, Func<T> action)
        {
            if (syncOrAsyncHalibut == SyncOrAsyncHalibut.Sync)
            {
                return new SyncOrAsyncWithResult<T>(syncOrAsyncHalibut, action());
            }

            return new SyncOrAsyncWithResult<T>(syncOrAsyncHalibut, default);
        }

        public static SyncOrAsyncWithoutResult IgnoreResult<T>(this SyncOrAsyncWithResult<T> syncOrAsyncWithResult)
        {
            return new(syncOrAsyncWithResult.SyncOrAsyncHalibut);
        }

        public static async Task<T> WhenAsync<T>(this SyncOrAsyncWithResult<T> syncOrAsyncWithResult, Func<Task<T>> action)
        {
            if (syncOrAsyncWithResult.SyncOrAsyncHalibut == SyncOrAsyncHalibut.Async)
            {
                return await action();
            }

            return syncOrAsyncWithResult.Result!;
        }
    }

    public class SyncOrAsyncWithoutResult
    {
        public SyncOrAsyncHalibut SyncOrAsyncHalibut { get; }

        public SyncOrAsyncWithoutResult(SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            SyncOrAsyncHalibut = syncOrAsyncHalibut;
        }
    }

    public class SyncOrAsyncWithResult<T>
    {
        public SyncOrAsyncHalibut SyncOrAsyncHalibut { get; }
        public T? Result { get; }

        public SyncOrAsyncWithResult(SyncOrAsyncHalibut syncOrAsyncHalibut, T? result)
        {
            SyncOrAsyncHalibut = syncOrAsyncHalibut;
            Result = result;
        }
    }
}
