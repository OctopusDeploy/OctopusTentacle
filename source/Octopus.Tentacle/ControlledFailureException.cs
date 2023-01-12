using System;

namespace Octopus.Tentacle
{
    /// <summary>
    /// A controlled failure is one whereby Octopus must abort the task at
    /// hand but does not need to reveal additional stack trace information to
    /// the user, e.g. when a script returns a failed exit code.
    /// </summary>
    /// <remarks>
    /// ONLY throw this exception if you're 100% certain that a stack trace
    /// won't be useful to the user when they try to diagnose the issue.
    /// </remarks>
    public class ControlledFailureException : Exception
    {
        public ControlledFailureException(string message)
            : base(message)
        {
        }

        public ControlledFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}