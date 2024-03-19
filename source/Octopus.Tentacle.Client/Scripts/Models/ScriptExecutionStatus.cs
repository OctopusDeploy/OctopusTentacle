using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ScriptExecutionStatus
    {
        public ScriptExecutionStatus(List<ProcessOutput> logs)
        {
            Logs = logs;
        }

        public List<ProcessOutput> Logs { get; }
    }
}