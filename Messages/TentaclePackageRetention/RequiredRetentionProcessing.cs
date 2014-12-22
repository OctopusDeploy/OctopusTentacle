using System;
using System.Collections.Generic;

namespace Octopus.Shared.Messages.TentaclePackageRetention
{
    public class RequiredRetentionProcessing
    {
        public string MachineId { get; private set; }
        public List<string> RetentionTokens { get; private set; }

        public RequiredRetentionProcessing(string machineId, List<string> retentionTokens)
        {
            MachineId = machineId;
            RetentionTokens = retentionTokens;
        }
    }
}