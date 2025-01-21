using System;

namespace Octopus.Tentacle.Client.Scripts
{
    /// <summary>
    /// This is thrown when we are attempting to start a script using a V1 script service, but we are trying to do so for the second time.
    /// For resilient deployments, we need to ensure idempotency for our operations, including starting a script.
    /// Idempotency was built into script service V2 onwards.
    /// If we attempt to use a V1 script service on the resilient pipeline, then we are allowed to start the script.
    /// But if anything goes wrong, and we need to reattempt, then we cannot guarantee the script has not already started.
    /// In this case, the safest action is to fail (with this exception).
    /// </summary>
    public class UnsafeStartAttemptException : Exception
    {
        public UnsafeStartAttemptException(string message) : base(message)
        {
            
        }
    }
}