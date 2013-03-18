using System;

namespace Octopus.Shared.Integration.Scripting
{
    [Serializable]
    public class ScriptFailureException : Exception
    {
        public ScriptFailureException(string message) : base(message)
        {
        }
    }
}