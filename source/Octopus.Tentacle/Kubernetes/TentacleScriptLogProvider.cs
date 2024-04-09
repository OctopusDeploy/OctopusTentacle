using System;
using System.Collections.Concurrent;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public class TentacleScriptLogProvider
    {
        readonly ConcurrentDictionary<ScriptTicket, InMemoryTentacleScriptLog> logs = new();

        //Tentacle might restart after a script has started, so we always have to create this logger on demand
        public InMemoryTentacleScriptLog GetOrCreate(ScriptTicket scriptTicket)
        {
            return logs.GetOrAdd(scriptTicket, _ => new InMemoryTentacleScriptLog());
        }

        //Having this clean up should reduce significant memory leaks
        //Assuming there's always a chance that GetOrCreate() might be called after Delete()
        public void Delete(ScriptTicket scriptTicket)
        {
            logs.TryRemove(scriptTicket, out _);
        }
    }
}