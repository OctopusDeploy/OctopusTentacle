using System;
using System.Collections.Concurrent;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

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
        const string PodLogsSinceTimeFilename = "last-pod-log-timestamp.txt";
        const string PodEventsSinceTimeFilename = "last-pod-event-timestamp.txt";

        readonly IScriptWorkspaceFactory workspaceFactory;

        readonly ConcurrentDictionary<ScriptTicket, Lazy<IScriptWorkspace>> workspaces = new();

        readonly ConcurrentDictionary<ScriptTicket, DateTimeOffset?> logsSinceTimes = new();
        readonly ConcurrentDictionary<ScriptTicket, DateTimeOffset?> eventsSinceTimes = new();

        public ScriptPodSinceTimeStore(IScriptWorkspaceFactory workspaceFactory)
        {
            this.workspaceFactory = workspaceFactory;
        }

        public DateTimeOffset? GetPodLogsSinceTime(ScriptTicket scriptTicket) => GetTimestampFromMemoryOrDisk(scriptTicket, logsSinceTimes, PodLogsSinceTimeFilename);

        public void UpdatePodLogsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime) => SaveTimestampInMemoryAndDisk(scriptTicket, nextSinceTime, logsSinceTimes, PodLogsSinceTimeFilename);

        public DateTimeOffset? GetPodEventsSinceTime(ScriptTicket scriptTicket) => GetTimestampFromMemoryOrDisk(scriptTicket, eventsSinceTimes, PodEventsSinceTimeFilename);

        public void UpdatePodEventsSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime) => SaveTimestampInMemoryAndDisk(scriptTicket, nextSinceTime, eventsSinceTimes, PodEventsSinceTimeFilename);

        IScriptWorkspace GetWorkspace(ScriptTicket scriptTicket) => workspaces.GetOrAdd(scriptTicket, new Lazy<IScriptWorkspace>(() => workspaceFactory.GetWorkspace(scriptTicket))).Value;

        DateTimeOffset? GetTimestampFromMemoryOrDisk(ScriptTicket scriptTicket, ConcurrentDictionary<ScriptTicket, DateTimeOffset?> memoryCache, string filename)
        {
            //if we have it in memory, all good
            if (memoryCache.TryGetValue(scriptTicket, out var log))
            {
                return log;
            }

            var workspace = GetWorkspace(scriptTicket);
            lock (workspace)
            {
                //otherwise try and load it from disk
                var sinceTimeStr = workspace.ReadFile(filename);
                var sinceTime = DateTimeOffset.TryParse(sinceTimeStr, out var dto) ? dto : (DateTimeOffset?)null;

                //if we have a value on disk, save it in memory
                if (sinceTime is not null)
                {
                    //we only update the value if it hasn't already been updated _somewhere else_ (which it shouldn't be able to)
                    memoryCache.TryUpdate(scriptTicket, sinceTime, null);
                }

                return sinceTime;
            }
        }
        void SaveTimestampInMemoryAndDisk(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime, ConcurrentDictionary<ScriptTicket, DateTimeOffset?> memoryCache, string podLogsSinceTimeFilename)
        {
            memoryCache[scriptTicket] = nextSinceTime;
            
            var workspace = GetWorkspace(scriptTicket);
            lock (workspace)
            {
                workspace.WriteFile(podLogsSinceTimeFilename, nextSinceTime.ToString("O"));
            }
        }

        public void Delete(ScriptTicket scriptTicket)
        {
            workspaces.TryRemove(scriptTicket, out _);
            logsSinceTimes.TryRemove(scriptTicket, out _);
            eventsSinceTimes.TryRemove(scriptTicket, out _);
        }
    }
}