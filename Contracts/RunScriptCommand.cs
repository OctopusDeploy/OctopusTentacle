using System;

namespace Octopus.Shared.Contracts
{
    public class RunScriptCommand
    {
        readonly string scriptBody;

        public RunScriptCommand(string scriptBody)
        {
            this.scriptBody = scriptBody;
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }
    }
}