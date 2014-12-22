using System;
using Pipefish;

namespace Octopus.Shared.Messages.Guidance
{
    public class FailureGuidanceReply : IMessage
    {
        public FailureGuidance Guidance { get; private set; }
        public bool ApplyToSimilarFailures { get; set; }

        public FailureGuidanceReply(FailureGuidance guidance, bool applyToSimilarFailures)
        {
            Guidance = guidance;
            ApplyToSimilarFailures = applyToSimilarFailures;
        }
    }
}