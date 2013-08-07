using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;
using Pipefish;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Platform.Deploy.Package
{
    [BeginsConversationEndedBy(typeof(TentaclePackageDeployedEvent), typeof(CompletionEvent))]
    public class TentacleDeployPackageCommand : IMessage
    {
        public PackageMetadata Package { get; private set; }
        public IList<Variable> Variables { get; private set; }
        public LoggerReference Logger { get; private set; }

        public TentacleDeployPackageCommand(PackageMetadata package, IList<Variable> variables, LoggerReference logger)
        {
            Package = package;
            Variables = variables;
            Logger = logger;
        }
    }
}
