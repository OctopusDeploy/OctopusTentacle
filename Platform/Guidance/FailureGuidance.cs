using System;

namespace Octopus.Shared.Platform.Guidance
{
    public enum FailureGuidance
    {
        Fail,   // Fails the whole operation, meaningless to "apply to all"
        Retry,  // Retry the item
        Ignore  // Ignore the item; if per-machine then mark the machine as ignored
    }
}