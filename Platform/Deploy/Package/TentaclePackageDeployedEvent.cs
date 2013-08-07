using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Deployment;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Package
{
    public class TentaclePackageDeployedEvent : IMessage
    {
        public IList<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentaclePackageDeployedEvent(IList<TentacleArtifact> createdArtifacts)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}