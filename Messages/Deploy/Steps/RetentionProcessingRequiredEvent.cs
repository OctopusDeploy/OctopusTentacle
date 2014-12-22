using System;
using System.Collections.Generic;
using Pipefish;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public class RetentionProcessingRequiredEvent : IMessage
    {
        public List<string> RetentionTokens { get; private set; }
        public string MachineId { get; private set; }

        public RetentionProcessingRequiredEvent(List<string> retentionTokens, string machineId)
        {
            RetentionTokens = retentionTokens;
            MachineId = machineId;
        }
    }
}