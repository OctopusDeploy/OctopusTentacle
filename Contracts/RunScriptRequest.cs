using System;

namespace Octopus.Shared.Contracts
{
    public class RunScriptRequest
    {
        public RunScriptRequest(string scriptBody)
        {
            ScriptBody = scriptBody;
        }

        public string ScriptBody { get; set; }
    }
}