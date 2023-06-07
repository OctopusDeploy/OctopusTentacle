using System;

namespace Octopus.Tentacle.Client.Scripts
{
    public abstract class ScriptObserverBackoffStrategy : IScriptObserverBackoffStrategy
    {
        readonly ExponentialBackoff exponentialBackoff;

        public ScriptObserverBackoffStrategy(
            TimeSpan minimumBackoffDelay,
            TimeSpan maximumBackoffDelay,
            double backoffBase)
        {
            exponentialBackoff = new ExponentialBackoff(minimumBackoffDelay, maximumBackoffDelay, backoffBase);
        }

        public TimeSpan GetBackoff(int iteration) => exponentialBackoff.Get(iteration++);
    }
}
