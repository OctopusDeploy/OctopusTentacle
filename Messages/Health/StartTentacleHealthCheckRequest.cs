using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Model;

namespace Octopus.Platform.Deployment.Messages.Health
{
    public class StartTentacleHealthCheckRequest : IReusableMessage
    {
        public LoggerReference Logger { get; private set; }
        public TimeSpan Timeout { get; set; }
        public string MachineId { get; private set; }
        public string Squid { get; private set; }
        public Uri ListeningTentacleUri { get; set; }
        public string ListeningTentacleThumbprint { get; set; }
        public CommunicationStyle CommunicationStyle { get; private set; }
        public string MachineDescription { get; private set; }

        public StartTentacleHealthCheckRequest(LoggerReference logger, TimeSpan timeout, string machineId, string squid, Uri listeningTentacleUri, string listeningTentacleThumbprint, CommunicationStyle communicationStyle, string machineDescription)
        {
            Logger = logger;
            Timeout = timeout;
            MachineId = machineId;
            Squid = squid;
            ListeningTentacleUri = listeningTentacleUri;
            ListeningTentacleThumbprint = listeningTentacleThumbprint;
            CommunicationStyle = communicationStyle;
            MachineDescription = machineDescription;
        }

        public IReusableMessage CopyForReuse(LoggerReference logger)
        {
            return new StartTentacleHealthCheckRequest(logger, Timeout, MachineId, Squid, ListeningTentacleUri, ListeningTentacleThumbprint, CommunicationStyle, MachineDescription);
        }
    }
}
