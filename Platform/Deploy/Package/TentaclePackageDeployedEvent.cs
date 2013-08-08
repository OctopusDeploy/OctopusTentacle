using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Deploy.Steps;

namespace Octopus.Shared.Platform.Deploy.Package
{
    public class TentaclePackageDeployedEvent : IMessageWithTentacleArtifacts
    {
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentaclePackageDeployedEvent(List<TentacleArtifact> createdArtifacts)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}