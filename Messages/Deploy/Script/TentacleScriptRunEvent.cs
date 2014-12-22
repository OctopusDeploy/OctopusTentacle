using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Messages.Deploy.Steps;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Deployment.Messages.Deploy.Script
{
    public class TentacleScriptRunEvent : IMessageWithTentacleArtifacts
    {
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }
        public VariableDictionary OutputVariables { get; private set; }
        public List<string> RetentionTokens { get; private set; }

        public TentacleScriptRunEvent(List<TentacleArtifact> createdArtifacts, VariableDictionary outputVariables)
        {
            CreatedArtifacts = createdArtifacts ?? new List<TentacleArtifact>();
            OutputVariables = outputVariables ?? new VariableDictionary();
            RetentionTokens = new List<string>();
        }
    }
}