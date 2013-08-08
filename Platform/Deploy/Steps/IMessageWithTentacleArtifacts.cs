using System;
using System.Collections.Generic;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Steps
{
    public interface IMessageWithTentacleArtifacts : IMessage
    {
        List<TentacleArtifact> CreatedArtifacts { get; }
    }
}