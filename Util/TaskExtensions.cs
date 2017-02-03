using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

namespace Octopus.Shared.Util
{
    public static class TaskExtensions
    {
        public static T SafeResult<T>(this Task<T> task) => task.GetAwaiter().GetResult();

        public static T SafeResult<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            try
            {
                task.Wait(cancellationToken);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ExceptionHelpers.PrepareForRethrow(ex.InnerException);
            }
        }

        public static void SafeResult(this Task task) => task.GetAwaiter().GetResult();

        public static void SafeResult(this Task task, CancellationToken cancellationToken)
        {
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException ex)
            {
                throw ExceptionHelpers.PrepareForRethrow(ex.InnerException);
            }
        }
    }
}