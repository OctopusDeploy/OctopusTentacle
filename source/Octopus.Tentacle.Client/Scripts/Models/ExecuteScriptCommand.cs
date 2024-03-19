using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteScriptCommand
    {
        public ExecuteScriptCommand(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationLevel isolationLevel,
            TimeSpan isolationMutexTimeout,
            string isolationMutexName,
            TimeSpan? durationToWaitForScriptToFinish = null,
            Dictionary<ScriptType, string>? additionalScripts = null,
            ScriptFile[]? additionalFiles = null)
        {
            ScriptBody = scriptBody;
            TaskId = taskId;
            ScriptTicket = scriptTicket;
            DurationToWaitForScriptToFinish = durationToWaitForScriptToFinish;
            IsolationLevel = isolationLevel;
            IsolationMutexTimeout = isolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
            Arguments = arguments;

            foreach (var additionalScript in additionalScripts ?? Enumerable.Empty<KeyValuePair<ScriptType, string>>())
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }

            if (additionalFiles is not null)
                Files.AddRange(additionalFiles);
        }

        public string ScriptBody { get;  }
        public string TaskId { get; }
        public ScriptTicket ScriptTicket { get;  }
        public TimeSpan? DurationToWaitForScriptToFinish { get;  }
        public ScriptIsolationLevel IsolationLevel { get;  }
        public TimeSpan IsolationMutexTimeout { get;  }
        public string IsolationMutexName { get;  }

        public Dictionary<ScriptType, string> Scripts { get; } = new();

        public List<ScriptFile> Files { get; } = new();

        public string[] Arguments { get;  }
    }
}