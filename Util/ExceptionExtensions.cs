using System;
using System.Reflection;
using System.Threading.Tasks;
using Octopus.Shared.Activities;

namespace Octopus.Shared.Util
{
    public static class ExceptionExtensions
    {
        public static Exception GetRootError(this Exception error)
        {
            if (error is AggregateException)
            {
                foreach (var item in ((AggregateException)error).InnerExceptions)
                {
                    return GetRootError(item);
                }
            }

            if (error is TargetInvocationException && error.InnerException != null)
            {
                return GetRootError(error.InnerException);
            }

            return error;
        }

        public static string GetErrorSummary(this Exception error)
        {
            error = error.GetRootError();

            if (error is TaskCanceledException)
                return "The task was canceled.";

            if (error is ActivityFailedException)
                return error.Message;

            return error.Message;
        }
    }
}