using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Halibut;
using Halibut.Diagnostics;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Communications
{
    public class HalibutInitializer : IHalibutInitializer
    {
        readonly IWritableTentacleConfiguration configuration;
        readonly HalibutRuntime halibut;
        readonly IProxyConfigParser proxyConfigParser;
        readonly ISystemLog log;

        public HalibutInitializer(IWritableTentacleConfiguration configuration, HalibutRuntime halibut, IProxyConfigParser proxyConfigParser, ISystemLog log)
        {
            this.configuration = configuration;
            this.halibut = halibut;
            this.proxyConfigParser = proxyConfigParser;
            this.log = log;
        }

        public void Start()
        {
            FixCommunicationStyle();

            TrustOctopusServers();

            AddPollingEndpoints();

            if (configuration.NoListen)
            {
                log.Info("Agent will not listen on any TCP ports");
                return;
            }

            var endpoint = GetEndPointToListenOn();

            halibut.Listen(endpoint);

            log.Info("Agent listening on: " + endpoint);
        }

        private void FixCommunicationStyle()
        {
            // https://github.com/OctopusDeploy/Issues/issues/2383
            if (configuration.NoListen)
                return;

            var invalidEntries = configuration.TrustedOctopusServers.Where(server => server.CommunicationStyle == CommunicationStyle.None).ToArray();
            if (invalidEntries.Length == 0)
                return;

            foreach (var server in invalidEntries)
            {
                server.CommunicationStyle = CommunicationStyle.TentaclePassive;
                configuration.AddOrUpdateTrustedOctopusServer(server);
                log.Info("Fixed communication style for: " + server.Thumbprint);
            }
        }

        void TrustOctopusServers()
        {
            var trust = GetTrustedOctopusThumbprints();
            foreach (var thumbprint in trust)
            {
                log.Info("Agent will trust Octopus Servers with the thumbprint: " + thumbprint);
                halibut.Trust(thumbprint);
            }

            if (trust.Count == 0)
            {
                log.Info("The agent is not configured to trust any Octopus Servers.");
            }
        }



        void AddPollingEndpoints()
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

                var connectionCount = GetPollingConnectionCount();

                for (var i = 0; i < connectionCount; i++)
                {
                    halibut.Poll(new Uri(pollingEndPoint.SubscriptionId), serviceEndPoint, CancellationToken.None);
                }
            }
        }
                
        const int MaximumPollingConnectionCount = 8;
                
        uint GetPollingConnectionCount()
        {
            //Open multiple polling connections if the env var is set to a non-zero/negative number
            var connectionCount = 1u;
            if (uint.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentaclePollingConnectionCount), out var count))
            {
                log.InfoFormat("Requested polling connection count: {0}", count);
                connectionCount = count;
            }

            //Coerce the requested value as it might be outside our max & min
            switch (connectionCount)
            {
                case > MaximumPollingConnectionCount:
                    log.InfoFormat("The requested polling connection count exceeds the maximum of {0}, limiting to {0}", MaximumPollingConnectionCount);
                    connectionCount = MaximumPollingConnectionCount;
                    break;
                case 0:
                    log.InfoFormat("The requested polling connection count must be greater than 0, setting to 1");
                    connectionCount = 1;
                    break;
            }

            log.InfoFormat("Starting {0} polling connections", connectionCount);
            return connectionCount;
        }

        List<string> GetTrustedOctopusThumbprints()
        {
            return configuration.TrustedOctopusServers.Select(t => t.Thumbprint).ToList();
        }

        IEnumerable<OctopusServerConfiguration> GetOctopusServersToPoll()
        {
            return configuration.TrustedOctopusServers.Where(octopusServerConfiguration =>
                octopusServerConfiguration.CommunicationStyle == CommunicationStyle.TentacleActive ||
                (octopusServerConfiguration is { CommunicationStyle: CommunicationStyle.KubernetesTentacle } &&
                    octopusServerConfiguration.KubernetesTentacleCommunicationMode == TentacleCommunicationModeResource.Polling));
        }

        IPEndPoint GetEndPointToListenOn()
        {
            var address = Socket.OSSupportsIPv6
                ? IPAddress.IPv6Any
                : IPAddress.Any;

            if (!string.IsNullOrWhiteSpace(configuration.ListenIpAddress))
            {
                address = IPAddress.Parse(configuration.ListenIpAddress);
            }

            return new IPEndPoint(address, configuration.ServicesPortNumber);
        }

        public void Stop()
        {
        }
    }
}
