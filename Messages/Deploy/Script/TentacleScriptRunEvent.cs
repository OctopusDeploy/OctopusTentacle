using System;
using System.Collections.Generic;
using Octopus.Shared.Messages.Deploy.Steps;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Messages.Deploy.Script
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