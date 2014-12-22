using System;
using Pipefish.Messages;

namespace Octopus.Platform.Deployment.Guidance
{
    public class FailedItem
    {
        public GuidedOperationItem Item { get; private set; }
        public Error Error { get; private set; }

        public FailedItem(GuidedOperationItem item, Error error)
        {
            Item = item;
            Error = error;
        }
    }
}