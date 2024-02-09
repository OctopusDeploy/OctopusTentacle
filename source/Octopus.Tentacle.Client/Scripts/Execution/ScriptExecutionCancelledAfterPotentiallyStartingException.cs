using System;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    public class ScriptExecutionCancelledAfterPotentiallyStartingException : Exception
    {
        public ScriptExecutionCancelledAfterPotentiallyStartingException(string message) : base(message)
        {
        }
    }
}