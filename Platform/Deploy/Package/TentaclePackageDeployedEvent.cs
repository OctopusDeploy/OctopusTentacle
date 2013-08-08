using System;
using System.Collections.Generic;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Package
{
    public class TentaclePackageDeployedEvent : IMessage
    {
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentaclePackageDeployedEvent(List<TentacleArtifact> createdArtifacts)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}