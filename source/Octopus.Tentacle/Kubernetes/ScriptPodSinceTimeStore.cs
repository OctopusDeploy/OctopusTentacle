using System;
using System.Collections.Concurrent;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IScriptPodSinceTimeStore
    {
        DateTimeOffset? GetSinceTime(ScriptTicket scriptTicket);
        void UpdateSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime);
        void Delete(ScriptTicket scriptTicket);
    }

    public class ScriptPodSinceTimeStore : IScriptPodSinceTimeStore
    {
        readonly ConcurrentDictionary<ScriptTicket, DateTimeOffset?> sinceTimes = new();
        
        public DateTimeOffset? GetSinceTime(ScriptTicket scriptTicket)
        {
            return sinceTimes.GetOrAdd(scriptTicket, _ => null);
        }

        public void UpdateSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime)
        {
            sinceTimes[scriptTicket] = nextSinceTime;
        }

        public void Delete(ScriptTicket scriptTicket)
        {
            sinceTimes.TryRemove(scriptTicket, out _);
        }
    }
}