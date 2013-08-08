using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Deploy.Steps;

namespace Octopus.Shared.Platform.Deploy.Script
{
    public class TentacleScriptRunEvent : IMessageWithTentacleArtifacts
    {
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentacleScriptRunEvent(List<TentacleArtifact> createdArtifacts)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}