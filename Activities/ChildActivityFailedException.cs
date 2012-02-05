using System;
using System.Collections.Generic;

namespace Octopus.Shared.Activities
{
    public class ChildActivityFailedException : AggregateException
    {
        public ChildActivityFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ChildActivityFailedException(string message, IEnumerable<Exception> innerExceptions) : base(message, innerExceptions)
        {
        }
    }
}