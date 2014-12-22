using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.Deploy.Script
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
