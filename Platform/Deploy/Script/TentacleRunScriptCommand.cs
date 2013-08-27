using System;
using System.Collections.Generic;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Script
{
    [ExpectReply]
    public class TentacleRunScriptCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public string Script { get; private set; }
        public List<Variable> Variables { get; private set; }

        public TentacleRunScriptCommand(LoggerReference logger, string script, List<Variable> variables)
        {
            Logger = logger;
            Script = script;
            Variables = variables;
        }
    }
}
