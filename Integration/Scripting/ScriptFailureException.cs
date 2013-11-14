using System;
using Octopus.Platform.Deployment;

namespace Octopus.Shared.Integration.Scripting
{
    public class ScriptFailureException : ControlledFailureException
    {
        public ScriptFailureException(string message) : base(message)
        {
        }
    }
}