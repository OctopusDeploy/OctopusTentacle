using System;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        readonly string scriptBody;
        readonly ScriptIsolationLevel isolation;

        public StartScriptCommand(string scriptBody) : this(scriptBody, null)
        {
        }

        [JsonConstructor]
        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation)
        {
            this.scriptBody = scriptBody;
            this.isolation = isolation ?? new NoIsolationLevel();
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }

        public ScriptIsolationLevel Isolation
        {
            get { return isolation; }
        }
    }

    public abstract class ScriptIsolationLevel
    {
    }

    public class NoIsolationLevel : ScriptIsolationLevel
    {
    }

    public class FullIsolationLevel : ScriptIsolationLevel
    {
    }
}