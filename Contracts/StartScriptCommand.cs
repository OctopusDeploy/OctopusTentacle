using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        readonly string[] arguments;
        readonly string scriptBody;
        readonly ScriptIsolationLevel isolation;
        readonly List<ScriptFile> files = new List<ScriptFile>();

        public StartScriptCommand(string scriptBody, params ScriptFile[] additionalFiles)
              : this(scriptBody, ScriptIsolationLevel.NoIsolation, new string[0], additionalFiles)
        {

        }

        public StartScriptCommand(string scriptBody, string[] arguments, params ScriptFile[] additionalFiles)
            : this(scriptBody, ScriptIsolationLevel.NoIsolation, arguments, additionalFiles)
        {
        }

        [JsonConstructor]
        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation)
        {
            this.scriptBody = scriptBody;
            this.isolation = isolation;
        }

        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, params ScriptFile[] additionalFiles)
            : this(scriptBody, isolation, new string[0], additionalFiles)
        {

        }

        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, string[] arguments, params ScriptFile[] additionalFiles)
            : this(scriptBody, isolation)
        {
            this.arguments = arguments;
            if (additionalFiles != null)
            {
                files.AddRange(additionalFiles);
            }
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }

        public ScriptIsolationLevel Isolation
        {
            get { return isolation; }
        }

        public List<ScriptFile> Files
        {
            get { return files; }
        }

        public string[] Arguments
        {
            get { return arguments; }
        }
    }
}