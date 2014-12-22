using System;
using Newtonsoft.Json;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Discovery
{
    public class SshEndpointDiscoveryReply : IMessage
    {
        public SshEndpointDiscovery SshEndpointDiscovery { get; private set; }
        public bool FoundEndpoint {
            get { return SshEndpointDiscovery != null; }
        }

        public SshEndpointDiscoveryReply(SshEndpointDiscovery sshEndpointDiscovery)
        {
            SshEndpointDiscovery = sshEndpointDiscovery;
        }
    }

    public class SshEndpointDiscovery
    {
        public string Host { get; private set; }
        public int Port { get; private set; }
        public string HostTentacleSquid { get; private set; }
        public string Fingerprint { get; private set; }

        [JsonConstructor]
        public SshEndpointDiscovery(string host, int port, string fingerprint)
        {
            Host = host;
            Port = port;
            Fingerprint = fingerprint;
        }

        public SshEndpointDiscovery(SshEndpointDiscovery sshEndpoint, string hostTentacleSquid)
        {
            Host = sshEndpoint.Host;
            Port = sshEndpoint.Port;
            HostTentacleSquid = hostTentacleSquid;
            Fingerprint = sshEndpoint.Fingerprint;
        }
    }

}