using System;
using System.Threading.Tasks;
using Halibut.Util;

namespace Octopus.Tentacle.Client.Utils
{
    public static class AsyncHalibutFeatureExtensionMethods
    {
        public static AsyncHalibutFeatureWithoutResult WhenDisabled(this AsyncHalibutFeature asyncHalibutFeature, Action action)
        {
            if (asyncHalibutFeature == AsyncHalibutFeature.Disabled)
            {
                action();
            }

            return new(asyncHalibutFeature);
        }

        public static AsyncHalibutFeatureWithResult<T> WhenDisabled<T>(this AsyncHalibutFeature asyncHalibutFeature, Func<T> action)
        {
            if (asyncHalibutFeature == AsyncHalibutFeature.Disabled)
            {
                return new AsyncHalibutFeatureWithResult<T>(asyncHalibutFeature, action());
            }

            return new AsyncHalibutFeatureWithResult<T>(asyncHalibutFeature, default);
        }

        public static void WhenEnabled(this AsyncHalibutFeatureWithoutResult asyncHalibutFeatureWithoutResult, Action action)
        {
            if (asyncHalibutFeatureWithoutResult.AsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                action();
            }
        }

        public static async Task WhenEnabled(this AsyncHalibutFeatureWithoutResult asyncHalibutFeatureWithoutResult, Func<Task> action)
        {
            if (asyncHalibutFeatureWithoutResult.AsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                await action();
            }
        }

        public static async Task<T> WhenEnabled<T>(this AsyncHalibutFeatureWithResult<T> asyncHalibutFeatureWithResult, Func<Task<T>> action)
        {
            if (asyncHalibutFeatureWithResult.AsyncHalibutFeature == AsyncHalibutFeature.Enabled)
            {
                return await action();
            }

            return asyncHalibutFeatureWithResult.Result!;
        }
    }

    public class AsyncHalibutFeatureWithoutResult
    {
        public AsyncHalibutFeature AsyncHalibutFeature { get; }

        public AsyncHalibutFeatureWithoutResult(AsyncHalibutFeature asyncHalibutFeature)
        {
            AsyncHalibutFeature = asyncHalibutFeature;
        }
    }

    public class AsyncHalibutFeatureWithResult<T>
    {
        public AsyncHalibutFeature AsyncHalibutFeature { get; }
        public T? Result { get; }

        public AsyncHalibutFeatureWithResult(AsyncHalibutFeature asyncHalibutFeature, T? result)
        {
            AsyncHalibutFeature = asyncHalibutFeature;
            Result = result;
        }
    }
}