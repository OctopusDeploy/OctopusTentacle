using System;
using System.Collections.Generic;
using Octopus.Shared.Communications.Conversations;
using Octopus.Shared.Contracts;
using Pipefish;

namespace Octopus.Shared.Orchestration.Deployment
{
    [BeginsConversationEndedBy(typeof(TentaclePackageDeploymentResult))]
    public class TentacleDeployPackageCommand : IMessage
    {
        public PackageMetadata Package { get; private set; }
        public IList<Variable> Variables { get; private set; }

        public TentacleDeployPackageCommand(PackageMetadata package, IList<Variable> variables)
        {
            Package = package;
            Variables = variables;
        }
    }
}
