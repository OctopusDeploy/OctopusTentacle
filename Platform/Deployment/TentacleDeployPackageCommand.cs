using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Platform.Deployment
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
