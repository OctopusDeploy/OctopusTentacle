using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.Discovery
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
