using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Deployment;

namespace Octopus.Shared.Platform.Deploy
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