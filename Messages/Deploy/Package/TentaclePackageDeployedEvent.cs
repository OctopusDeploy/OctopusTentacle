using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.Deploy.Package
{
    public class TentaclePackageDeployedEvent : IMessageWithTentacleArtifacts
    {
        public List<string> RetentionTokens { get; private set; }
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }
        public VariableDictionary OutputVariables { get; private set; }

        public TentaclePackageDeployedEvent(List<TentacleArtifact> createdArtifacts, VariableDictionary outputVariables, List<string> retentionTokens = null)
        {
            RetentionTokens = retentionTokens ?? new List<string>();
            CreatedArtifacts = createdArtifacts ?? new List<TentacleArtifact>();
            OutputVariables = outputVariables ?? new VariableDictionary();
        }
    }
}