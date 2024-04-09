using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public class InMemoryTentacleScriptLog
    {
        readonly List<ProcessOutput> logs = new();
        
        public void Verbose(string message)
        {
            lock (logs)
                logs.Add(new ProcessOutput(ProcessOutputSource.Debug, message));
        }

        public void Info(string message)
        {
            lock (logs)
                logs.Add(new ProcessOutput(ProcessOutputSource.StdOut, message));
        }

        public void Error(string message)
        {
            lock (logs)
                logs.Add(new ProcessOutput(ProcessOutputSource.StdErr, message));
        }

        public IReadOnlyCollection<ProcessOutput> PopLogs()
        {
            lock (logs)
            {
                var copy = logs.ToList();
                logs.Clear();
                return copy;
            }
        }
    }
}