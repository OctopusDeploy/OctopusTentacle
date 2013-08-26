using System;

namespace Octopus.Shared.Orchestration.Guidance
{
    public class FailedItem
    {
        public GuidedOperationItem Item { get; private set; }
        public string Error { get; private set; }
        public Exception Exception { get; private set; }

        public FailedItem(GuidedOperationItem item, string error, Exception exception)
        {
            Item = item;
            Error = error;
            Exception = exception;
        }
    }
}