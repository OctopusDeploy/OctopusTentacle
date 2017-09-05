using System;
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

            Options.Add("server-comms-port=", "The comms port on the Octopus server; the default is " + commsPort, s => commsPort = int.Parse(s));
            Options.Add("server-web-socket=", "When using active communication over websockets, the address of the Octopus server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => serverWebSocketAddress = s);
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

            using (var client = await octopusClientInitializer.CreateAsyncClient(api, proxyOverride))
            {
                var repository = new OctopusAsyncRepository(client);

                var alreadyConfiguredServerInCluster = await GetAlreadyConfiguredServerInCluster(repository);
                if (alreadyConfiguredServerInCluster == null)
                    return;

                var serverThumbprint = await GetServerThumbprint(repository, serverAddress, sslThumbprint);

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

        async Task<OctopusServerConfiguration> GetAlreadyConfiguredServerInCluster(IOctopusAsyncRepository repository)
        {
            if (!configuration.Value.TrustedOctopusServers.Any())
            {
                log.Error("No trusted Octopus Servers have been configure.  First register this Tentacle with the Octopus Server by using the register-with command.");
                return null;
            }
            var tentaclesWithMatchingThumbprints = await repository.Machines.FindByThumbprint(configuration.Value.TentacleCertificate.Thumbprint);
            if (!tentaclesWithMatchingThumbprints.Any())
            {
                log.Error("This Tentacle has not been registered with the server you are attempting to poll.  First register this Tentacle with the Octopus Server by using the register-with command.");
                return null;
            }

            foreach (var octopusServerConfiguration in configuration.Value.TrustedOctopusServers)
            {
                if (tentaclesWithMatchingThumbprints.Select(tentacleWithMatchingThumbprint => tentacleWithMatchingThumbprint.Endpoint)
                    .OfType<PollingTentacleEndpointResource>()
                    .Any(pollingEndpoint => octopusServerConfiguration.SubscriptionId == pollingEndpoint.Uri))
                {
                    if (octopusServerConfiguration.CommunicationStyle != CommunicationStyle.TentacleActive)
                    {
                        log.Error("This Tentacle has been registered with an Octopus Server in the same cluster but it is not in polling mode.");
                        continue;
                    }

                    return octopusServerConfiguration;
                }
            }

            log.Error("This Tentacle does not appear to trust an Octopus Server in the same cluster as the Octopus Server you are attempting to poll.  First register this Tentacle with the Octopus Server by using the register-with command.");
            return null;
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