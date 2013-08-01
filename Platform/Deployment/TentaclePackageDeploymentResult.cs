using System;
using System.Collections.Generic;

namespace Octopus.Shared.Orchestration.Deployment
{
    public class TentaclePackageDeploymentResult : ResultMessage
    {
        public IList<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentaclePackageDeploymentResult(bool wasSuccessful, string details, IList<TentacleArtifact> createdArtifacts)
            : base(wasSuccessful, details)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}