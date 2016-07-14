using System;

namespace Octopus.Shared.Tasks
{
    public class ActionFailedException : Exception
    {
        public ActionFailedException(string actionDescription, Exception innerException) : base(actionDescription, innerException)
        {
        }
    }
}