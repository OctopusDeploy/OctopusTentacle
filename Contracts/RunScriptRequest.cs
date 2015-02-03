using System;

namespace Octopus.Shared.Contracts
{
    public class RunScriptRequest
    {
        readonly string scriptBody;
        readonly string syntax;

        public RunScriptRequest(string scriptBody, string syntax)
        {
            this.scriptBody = scriptBody;
            this.syntax = syntax;
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }

        public string Syntax
        {
            get { return syntax; }
        }
    }
}