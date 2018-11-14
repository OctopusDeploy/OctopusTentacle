using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Halibut;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class PollCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly Lazy<IOctopusServerChecker> octopusServerChecker;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ILog log;
        readonly ApiEndpointOptions api;
        int commsPort = 10943;
        string serverWebSocketAddress;

        public PollCommand(Lazy<ITentacleConfiguration> configuration, 
                           ILog log, 
                           IApplicationInstanceSelector selector, 
                           Lazy<IOctopusServerChecker> octopusServerChecker, 
                           IProxyConfigParser proxyConfig,
                           IOctopusClientInitializer octopusClientInitializer)
            : base(selector)
        {
            this.configuration = configuration;
            this.octopusServerChecker = octopusServerChecker;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.log = log;

            api = AddOptionSet(new ApiEndpointOptions(Options));

            Options.Add("server-comms-port=", "The comms port on the Octopus Server; the default is " + commsPort, s => commsPort = int.Parse(s));
            Options.Add("server-web-socket=", "When using active communication over websockets, the address of the Octopus Server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => serverWebSocketAddress = s);
        }

        protected override void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        { 
            var serverAddress = GetAddress();

            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            var proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);

            string sslThumbprint = octopusServerChecker.Value.CheckServerCommunicationsIsOpen(serverAddress, proxyOverride);

            log.Info($"Registering the tentacle with the server at {api.ServerUri}");

            using (var client = await octopusClientInitializer.CreateClient(api, proxyOverride))
            {
                var repository = new OctopusAsyncRepository(client);

                var serverThumbprint = await GetServerThumbprint(repository, serverAddress, sslThumbprint);

                var alreadyConfiguredServerInCluster = GetAlreadyConfiguredServerInCluster(serverThumbprint);

                var octopusServerConfiguration = new OctopusServerConfiguration(serverThumbprint)
                {
                    Address = serverAddress,
                    CommunicationStyle = CommunicationStyle.TentacleActive,
                    SubscriptionId = alreadyConfiguredServerInCluster.SubscriptionId
                };

                configuration.Value.AddOrUpdateTrustedOctopusServer(octopusServerConfiguration);
                VoteForRestart();

                log.Info("Polling endpoint configured");
            }
        }

        OctopusServerConfiguration GetAlreadyConfiguredServerInCluster(string serverThumbprint)
        {
            var alreadyConfiguredServersInCluster = configuration.Value.TrustedOctopusServers
                .Where(s => s.Thumbprint == serverThumbprint)
                .ToArray();

            if (!alreadyConfiguredServersInCluster.Any())
            {
                throw new ControlledFailureException("The thumbprint of this server is not trusted. You must first trust this server using the configure command.");
            }

            OctopusServerConfiguration pollingServerConfiguration = alreadyConfiguredServersInCluster
                .FirstOrDefault(c => c.CommunicationStyle == CommunicationStyle.TentacleActive && c.SubscriptionId != null);
            if (pollingServerConfiguration == null)
            {
                throw new ControlledFailureException("This server has not been configured as a polling tentacle. Reconfigure the server as a polling tentacle using the server-comms command.");
            }

            return pollingServerConfiguration;
        }

        async Task<string> GetServerThumbprint(IOctopusAsyncRepository repository, Uri serverAddress, string sslThumbprint)
        {
            if (serverAddress != null && ServiceEndPoint.IsWebSocketAddress(serverAddress))
            {
                if (sslThumbprint == null)
                    throw new Exception($"Could not determine thumbprint of the SSL Certificate at {serverAddress}");
                return sslThumbprint;
            }
            return (await repository.CertificateConfiguration.GetOctopusCertificate()).Thumbprint;
        }

        Uri GetAddress()
        {
            if (string.IsNullOrWhiteSpace(serverWebSocketAddress))
                return new Uri("https://" + api.ServerUri.Host + ":" + commsPort);

            if (!HalibutRuntime.OSSupportsWebSockets)
                throw new Exception("Websockets is only supported on Windows Server 2012 and later");

            var address = new Uri(serverWebSocketAddress);

            switch (address.Scheme.ToLower())
            {
                case "https":
                    address = new UriBuilder(address) { Scheme = "wss" }.Uri;
                    break;
                case "wss":
                    break;
                default:
                    throw new ControlledFailureException("The websocket address must start with wss://");
            }

            return address;
        }
    }
}