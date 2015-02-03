using System;

namespace Octopus.Shared.Tasks
{
    public class ActivityFailedException : Exception
    {
        public ActivityFailedException(string message) : base(message)
        {
        }
    }
}