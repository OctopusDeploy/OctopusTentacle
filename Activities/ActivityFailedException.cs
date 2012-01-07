using System;

namespace Octopus.Shared.Activities
{
    public class ActivityFailedException : Exception
    {
        public ActivityFailedException(string message)
            : base(message)
        {

        }
    }
}