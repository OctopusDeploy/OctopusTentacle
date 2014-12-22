using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Deploy.Script;
using Octopus.Platform.Model;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.RunScript
{
    public class RunScriptOnTentacleCommand : ICorrelatedMessage
    {
        public string MachineId { get; private set; }
        public LoggerReference Logger { get; private set; }
        public string ScriptBody { get; private set; }
        public ScriptSyntax Syntax { get; private set; }
        public List<Variable> Variables { get; private set; }
        public ReferenceCollection RelatedDocumentIds { get; private set; }

        public RunScriptOnTentacleCommand(LoggerReference logger, string machineId, string scriptBody, ScriptSyntax syntax, List<Variable> variables, ReferenceCollection relatedDocumentIds)
        {
            Logger = logger;
            MachineId = machineId;
            ScriptBody = scriptBody;
            Syntax = syntax;
            Variables = variables;
            RelatedDocumentIds = relatedDocumentIds;
        }
    }
}
