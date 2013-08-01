using System;
using Pipefish;

namespace Octopus.Shared.Platform
{
    public abstract class ResultMessage : IMessage
    {
        public bool WasSuccessful { get; private set; }
        public string Details { get; private set; }

        protected ResultMessage(bool wasSuccessful, string details)
        {
            WasSuccessful = wasSuccessful;
            Details = details;
        }
    }
}