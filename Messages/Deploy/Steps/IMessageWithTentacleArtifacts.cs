using System;
using System.Collections.Generic;
using Octopus.Platform.Variables;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Deploy.Steps
{
    public interface IMessageWithTentacleArtifacts : IMessage
    {
        List<TentacleArtifact> CreatedArtifacts { get; }
        VariableDictionary OutputVariables { get; }
        List<string> RetentionTokens { get; }
    }
}