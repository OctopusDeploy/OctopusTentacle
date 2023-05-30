using System;

namespace Octopus.Tentacle.Client.Retries
{
    internal class OperationAbandonedException : OperationCanceledException
    {
        public OperationAbandonedException(Exception inner, TimeSpan abandonedAfter) : base($"The operation was abandoned as it did not cancel after {abandonedAfter}", inner)
        {
        }
    }
}