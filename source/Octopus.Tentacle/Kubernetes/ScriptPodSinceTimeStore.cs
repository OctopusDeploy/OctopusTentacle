using System;
using System.Collections.Concurrent;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IScriptPodSinceTimeStore
    {
        DateTimeOffset? GetPodLogsSinceTime(ScriptTicket scriptTicket);
        void UpdatePodLogsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime);
        DateTimeOffset? GetPodEventsSinceTime(ScriptTicket scriptTicket);
        void UpdatePodEventsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime);
        void Delete(ScriptTicket scriptTicket);
    }

    public class ScriptPodSinceTimeStore : IScriptPodSinceTimeStore
    {
        readonly ConcurrentDictionary<ScriptTicket, DateTimeOffset?> logsSinceTimes = new();
        readonly ConcurrentDictionary<ScriptTicket, DateTimeOffset?> eventsSinceTimes = new();
        
        public DateTimeOffset? GetPodLogsSinceTime(ScriptTicket scriptTicket) => logsSinceTimes.GetOrAdd(scriptTicket, _ => null);

        public void UpdatePodLogsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime) => logsSinceTimes[scriptTicket] = nextSinceTime;

        public DateTimeOffset? GetPodEventsSinceTime(ScriptTicket scriptTicket) => eventsSinceTimes.GetOrAdd(scriptTicket, _ => null);

        public void UpdatePodEventsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime)=> eventsSinceTimes[scriptTicket] = nextSinceTime;

        public void Delete(ScriptTicket scriptTicket)
        {
            logsSinceTimes.TryRemove(scriptTicket, out _);
            eventsSinceTimes.TryRemove(scriptTicket, out _);
        }
    }
}