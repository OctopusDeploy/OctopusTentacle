using System;

namespace Octopus.Tentacle.Time
{
    public static class ExponentialBackoff
    {
        //retryAttempt is 1 for the first retry
        public static int GetDuration(int retryAttempt, int maxDuration)
        {
            return (int)Math.Min(maxDuration, Math.Pow(2, retryAttempt-1));
        }
    }
}