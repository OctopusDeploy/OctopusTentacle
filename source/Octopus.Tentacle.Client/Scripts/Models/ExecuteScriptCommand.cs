using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public abstract class ExecuteScriptCommand
    {
        protected ExecuteScriptCommand(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationConfiguration isolationConfiguration,
            Dictionary<ScriptType, string>? additionalScripts = null,
            ScriptFile[]? additionalFiles = null)
        {
            ScriptBody = scriptBody;
            TaskId = taskId;
            ScriptTicket = scriptTicket;
            Arguments = arguments;
            IsolationConfiguration = isolationConfiguration;

            foreach (var additionalScript in additionalScripts ?? Enumerable.Empty<KeyValuePair<ScriptType, string>>())
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }

            if (additionalFiles is not null)
                Files.AddRange(additionalFiles);
        }

        public string ScriptBody { get;  }
        public string[] Arguments { get;  }
        public string TaskId { get; }
        public ScriptTicket ScriptTicket { get;  }
        public ScriptIsolationConfiguration IsolationConfiguration { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();

        public List<ScriptFile> Files { get; } = new();

    }
}