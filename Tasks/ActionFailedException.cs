using System;

namespace Octopus.Shared.Tasks
{
    public class ActionFailedException : Exception
    {
        public ActionFailedException(string actionName, Exception innerException) : base(actionName, innerException)
        {
        }
    }
}