using System;
using Octopus.Client.Model;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class TentacleManagerScriptExecuted : TelemetryEvent
    {
        public TentacleManagerScriptExecuted(
            string userId,
            string deviceId,
            string description,
            MachineType machineType,
            CommunicationStyle communicationStyle)
            : base("Tentacle Manager script executed", userId, deviceId)
        {
            EventProperties.Add("Description", description);
            EventProperties.Add("Machine Type", machineType.ToString());
            EventProperties.Add("Communication Style", communicationStyle.ToString());
        }
    }
}
