using System;
using System.Collections.Generic;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Script
{
    public class TentacleScriptRunEvent : IMessage
    {
        public List<TentacleArtifact> CreatedArtifacts { get; private set; }

        public TentacleScriptRunEvent(List<TentacleArtifact> createdArtifacts)
        {
            CreatedArtifacts = createdArtifacts;
        }
    }
}