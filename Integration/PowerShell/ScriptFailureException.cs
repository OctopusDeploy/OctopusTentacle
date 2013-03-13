using System;

namespace Octopus.Shared.Integration.PowerShell
{
    [Serializable]
    public class ScriptFailureException : Exception
    {
        public ScriptFailureException(string message) : base(message)
        {
        }
    }
}