using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;

namespace Octopus.Platform.Deployment.Messages.Discovery
{
    [ExpectReply]
    public class SshEndpointDiscoveryRequest : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string Host { get; private set; }
        public int Port { get; private set; }

        public SshEndpointDiscoveryRequest(LoggerReference logger, string host, int port)
        {
            Logger = logger;
            Host = host;
            Port = port;
        }
    }
}
