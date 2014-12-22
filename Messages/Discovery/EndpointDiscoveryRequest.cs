using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Discovery
{
    public class EndpointDiscoveryRequest : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string ExpectedThumbprint { get; private set; }

        public EndpointDiscoveryRequest(LoggerReference logger, string host, int port, string expectedThumbprint = null)
        {
            Logger = logger;
            Host = host;
            Port = port;
            ExpectedThumbprint = expectedThumbprint;
        }
    }
}
