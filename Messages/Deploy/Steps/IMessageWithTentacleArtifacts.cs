using System;
using System.Collections.Generic;
using Octopus.Shared.Variables;
using Pipefish;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public interface IMessageWithTentacleArtifacts : IMessage
    {
        List<TentacleArtifact> CreatedArtifacts { get; }
        VariableDictionary OutputVariables { get; }
        List<string> RetentionTokens { get; }
    }
}