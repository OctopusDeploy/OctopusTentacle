using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Deploy;
using Octopus.Shared.Platform.Logging;
using Pipefish;

namespace Octopus.Shared.Platform.Deployment
{
    [BeginsConversationEndedBy(typeof(TentaclePackageDeploymentResult))]
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
