using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        readonly string scriptBody;
        readonly ScriptIsolationLevel isolation;
        readonly List<ScriptFile> files = new List<ScriptFile>();

        public StartScriptCommand(string scriptBody, params ScriptFile[] additionalFiles)
            : this(scriptBody, ScriptIsolationLevel.NoIsolation, additionalFiles)
        {
        }

        [JsonConstructor]
        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation)
        {
            this.scriptBody = scriptBody;
            this.isolation = isolation;
        }

        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, params ScriptFile[] additionalFiles)
            : this(scriptBody, isolation)
        {
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
    }
}