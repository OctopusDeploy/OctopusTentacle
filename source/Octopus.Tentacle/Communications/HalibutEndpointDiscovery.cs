using System;
using System.Collections.Generic;
using System.Linq;
using Halibut;
using Halibut.Diagnostics;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Communications
{
    public class HalibutEndpointDiscovery
    {
        readonly IWritableTentacleConfiguration configuration;
        readonly IProxyConfigParser proxyConfigParser;
        readonly ISystemLog log;

        public HalibutEndpointDiscovery(IWritableTentacleConfiguration configuration, IProxyConfigParser proxyConfigParser, ISystemLog log)
        {
            this.configuration = configuration;
            this.proxyConfigParser = proxyConfigParser;
            this.log = log;
        }

        public IEnumerable<ServiceEndPoint> GetPollingEndpoints()
        {
            foreach (var pollingEndPoint in GetOctopusServersToPoll())
            {
                if (pollingEndPoint.Address == null)
                {
                    log.WarnFormat("Configured to connect to server {0}, but its configuration is incomplete; skipping.", pollingEndPoint);
                    continue;
                }

#pragma warning disable 618
                pollingEndPoint.SubscriptionId ??= "poll://" + configuration.TentacleSquid?.ToLowerInvariant() + "/";
#pragma warning restore 618

                log.Info($"Agent will poll Octopus Server at {pollingEndPoint.Address} for subscription {pollingEndPoint.SubscriptionId} expecting thumbprint {pollingEndPoint.Thumbprint}");
                var halibutProxy = proxyConfigParser.ParseToHalibutProxy(configuration.PollingProxyConfiguration, pollingEndPoint.Address, log);

                var halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimits();
                var serviceEndPoint = new ServiceEndPoint(pollingEndPoint.Address, pollingEndPoint.Thumbprint, halibutProxy, halibutTimeoutsAndLimits);

                yield return serviceEndPoint;
            }
        }
        
        IEnumerable<OctopusServerConfiguration> GetOctopusServersToPoll()
        {
            return configuration.TrustedOctopusServers.Where(octopusServerConfiguration =>
                octopusServerConfiguration.CommunicationStyle == CommunicationStyle.TentacleActive ||
                (octopusServerConfiguration is { CommunicationStyle: CommunicationStyle.KubernetesTentacle } &&
                    octopusServerConfiguration.KubernetesTentacleCommunicationMode == TentacleCommunicationModeResource.Polling));
        }
    }
}