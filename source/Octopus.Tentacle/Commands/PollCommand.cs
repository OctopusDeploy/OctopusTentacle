using System;
using System.Linq;
using System.Threading.Tasks;
using Halibut;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class PollCommand : AbstractStandardCommand
    {
        const int DefaultServerCommsPort = 10943;

        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly Lazy<IOctopusServerChecker> octopusServerChecker;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISystemLog log;
        readonly IApplicationInstanceSelector selector;
        readonly ApiEndpointOptions api;
        int? serverCommsPort = null;
        string serverWebSocketAddress = null!;
        string serverCommsAddress = null!;
        bool reuseThumbprint = false;

        public PollCommand(Lazy<IWritableTentacleConfiguration> configuration,
                           ISystemLog log,
                           IApplicationInstanceSelector selector,
                           Lazy<IOctopusServerChecker> octopusServerChecker,
                           IProxyConfigParser proxyConfig,
                           IOctopusClientInitializer octopusClientInitializer,
                           ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.octopusServerChecker = octopusServerChecker;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.log = log;
            this.selector = selector;

            api = AddOptionSet(new ApiEndpointOptions(Options, allowBypass: true));

            Options.Add("server-comms-address=", "The comms address on the Octopus Server; the address of the Octopus Server will be used if omitted.", s => serverCommsAddress = s);
            Options.Add("server-comms-port=", "The comms port on the Octopus Server; the default is " + DefaultServerCommsPort + ". If specified, this will take precedence over any port number in server-comms-address.", s => serverCommsPort = int.Parse(s));
            Options.Add("server-web-socket=", "When using active communication over websockets, the address of the Octopus Server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => serverWebSocketAddress = s);
            Options.Add("reuse-server-thumbprint", "Reuse the Server Thumbprint from the first trusted server instance currently configured", _ => reuseThumbprint = true);
        }

        protected override void Start()
        {
            base.Start();
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            if (!string.IsNullOrEmpty(serverWebSocketAddress) && !string.IsNullOrEmpty(serverCommsAddress))
                throw new ControlledFailureException("Please specify a --server-web-socket, or a --server-comms-address - not both.");

            var serverAddress = GetAddress();

            var serverConfiguration = reuseThumbprint ?
                CreateServerConfigurationFromFirstTrustedServer(serverAddress) :
                await CreateServerConfigurationViaServerAPI(serverAddress);

            configuration.Value.AddOrUpdateTrustedOctopusServer(serverConfiguration);
            VoteForRestart();

            log.Info("Polling endpoint configured");
        }

        OctopusServerConfiguration CreateServerConfigurationFromFirstTrustedServer(Uri serverAddress)
        {
            var firstTrustedServer = configuration.Value.TrustedOctopusServers.First();
            return new OctopusServerConfiguration(firstTrustedServer.Thumbprint)
            {
                Address = serverAddress,
                CommunicationStyle = firstTrustedServer.CommunicationStyle,
                SubscriptionId = firstTrustedServer.SubscriptionId
            };
        }

        async Task<OctopusServerConfiguration> CreateServerConfigurationViaServerAPI(Uri serverAddress)
        {
            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            var proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);

            var sslThumbprint = octopusServerChecker.Value.CheckServerCommunicationsIsOpen(serverAddress, proxyOverride);

            log.Info($"Configuring Tentacle to poll the server at {api.ServerUri}");

            using var client = await octopusClientInitializer.CreateClient(api, proxyOverride);

            var repository = new OctopusAsyncRepository(client);

            var serverThumbprint = await GetServerThumbprint(repository, serverAddress, sslThumbprint);

            var alreadyConfiguredServerInCluster = GetAlreadyConfiguredServerInCluster(serverThumbprint);

            return new OctopusServerConfiguration(serverThumbprint)
            {
                Address = serverAddress,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                SubscriptionId = alreadyConfiguredServerInCluster.SubscriptionId
            };
        }

        OctopusServerConfiguration GetAlreadyConfiguredServerInCluster(string serverThumbprint)
        {
            var alreadyConfiguredServersInCluster = configuration.Value.TrustedOctopusServers
                .Where(s => s.Thumbprint == serverThumbprint)
                .ToArray();

            var executable = PlatformDetection.IsRunningOnWindows ? "Tentacle.exe" : "Tentacle";
            var instanceArg = selector.Current.InstanceName == null ? "" : $" --instance {selector.Current.InstanceName}";

            if (!alreadyConfiguredServersInCluster.Any())
            {
                throw new ControlledFailureException($"The Octopus Server with the thumbprint '{serverThumbprint}' is not yet trusted. " + Environment.NewLine +
                    $"Trust this Octopus Server using '{executable} configure --trust=\"{serverThumbprint}\"{instanceArg}'");
            }

            var pollingServerConfiguration = alreadyConfiguredServersInCluster
                .FirstOrDefault(c => c.CommunicationStyle == CommunicationStyle.TentacleActive && c.SubscriptionId != null);
            if (pollingServerConfiguration == null)
            {
                throw new ControlledFailureException("This Tentacle has not been configured to connect to the specified Octopus Server as a polling Tentacle. " + Environment.NewLine +
                    $"Reconfigure this Tentacle to poll the server using either:" + Environment.NewLine +
                    $"'{executable} server-comms --thumbprint=\"{serverThumbprint}\" --style=TentacleActive{instanceArg} --host {new Uri(api.Server).Host}' or " + Environment.NewLine +
                    $"'{executable} server-comms --thumbprint=\"{serverThumbprint}\" --style=TentacleActive{instanceArg} --web-socket <web-socket-address>'");
            }

            return pollingServerConfiguration;
        }

        async Task<string> GetServerThumbprint(IOctopusAsyncRepository repository, Uri serverAddress, string sslThumbprint)
        {
            if (serverAddress != null && ServiceEndPoint.IsWebSocketAddress(serverAddress))
            {
                if (sslThumbprint == null)
                    throw new ControlledFailureException($"Could not determine thumbprint of the SSL Certificate at {serverAddress}");
                return sslThumbprint;
            }
            return (await repository.CertificateConfiguration.GetOctopusCertificate()).Thumbprint;
        }

        Uri GetAddress()
        {
            if (string.IsNullOrWhiteSpace(serverWebSocketAddress))
            {
                Uri serverCommsAddressUri;

                if (string.IsNullOrEmpty(serverCommsAddress))
                {
                    if (reuseThumbprint) throw new InvalidOperationException("You must specify either a WebSocketAddress or a ServerCommsAddress");

                    serverCommsAddressUri = api.ServerUri;
                    serverCommsPort ??= DefaultServerCommsPort;
                }
                else
                {
                    serverCommsAddressUri = new Uri(serverCommsAddress);
                    serverCommsPort ??= serverCommsAddressUri.Port;
                }

                return new Uri($"https://{serverCommsAddressUri.Host}:{serverCommsPort}");
            }

            if (!HalibutRuntime.OSSupportsWebSockets)
                throw new ControlledFailureException("Websockets is only supported on Windows Server 2012 and later");

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
