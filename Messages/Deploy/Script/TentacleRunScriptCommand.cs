using System;
using System.Collections.Generic;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Messages.Deploy.Script
{
    [ExpectReply]
    public class TentacleRunScriptCommand : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }

        public string Script { get; private set; }
        public List<Variable> Variables { get; private set; }
        public ScriptSyntax Syntax { get; private set; }

        public TentacleRunScriptCommand(LoggerReference logger, string script, ScriptSyntax syntax, List<Variable> variables)
        {
            Syntax = syntax;
            Logger = logger;
            Script = script;
            Variables = variables;
        }

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new TentacleRunScriptCommand(newLogger, Script, Syntax, Variables);
        }
    }
}
