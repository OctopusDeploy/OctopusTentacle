using System;
using System.Reflection;

namespace Octopus.Shared.Util
{
    public static class ExceptionExtensions
    {
        public static Exception UnpackFromContainers(this Exception error)
        {
            if (error is AggregateException aggregateException && aggregateException.InnerExceptions.Count == 1)
                return UnpackFromContainers(aggregateException.InnerExceptions[0]);

            if (error is TargetInvocationException && error.InnerException != null)
                return UnpackFromContainers(error.InnerException);

            return error;
        }
    }
}