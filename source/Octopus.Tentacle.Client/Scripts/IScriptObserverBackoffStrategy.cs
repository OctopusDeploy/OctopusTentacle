using System;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IScriptObserverBackoffStrategy
    {
        TimeSpan GetBackoff(int iteration);
    }
}
