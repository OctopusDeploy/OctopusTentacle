using System;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        readonly string scriptBody;

        public StartScriptCommand(string scriptBody)
        {
            this.scriptBody = scriptBody;
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }
    }
}