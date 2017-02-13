using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        [JsonConstructor]
        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, TimeSpan scriptIsolationMutexTimeout, string[] arguments)
        {
            Arguments = arguments;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
        }

        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, TimeSpan scriptIsolationMutexTimeout, string[] arguments, params ScriptFile[] additionalFiles)
            : this(scriptBody, isolation, scriptIsolationMutexTimeout, arguments)
        {
            if (additionalFiles != null)
            {
                Files.AddRange(additionalFiles);
            }
        }

        public string ScriptBody { get; }

        public ScriptIsolationLevel Isolation { get; }

        public List<ScriptFile> Files { get; } = new List<ScriptFile>();

        public string[] Arguments { get; }

        public TimeSpan ScriptIsolationMutexTimeout { get; }
    }
}