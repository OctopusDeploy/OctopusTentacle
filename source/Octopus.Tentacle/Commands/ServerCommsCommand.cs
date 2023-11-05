using System;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class ServerCommsCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> tentacleConfiguration;
        readonly ISystemLog log;
        string serverThumbprint = null!;
        string style = null!;
        string serverHost = null!;
        int serverPort = 10943;
        string webSocket = null!;

        public ServerCommsCommand(Lazy<IWritableTentacleConfiguration> tentacleConfiguration, ISystemLog log, IApplicationInstanceSelector selector, ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("thumbprint=", "The thumbprint of the Octopus Server to configure communication with; if only one Octopus Server is configured, this may be omitted", s => serverThumbprint = s);
            Options.Add("style=", "The communication style to use with the Octopus Server - either TentacleActive or TentaclePassive", s => style = s);
            Options.Add("host=", "When using active communication, the host name of the Octopus Server", s => serverHost = s);
            Options.Add("port=", "When using active communication, the communications port of the Octopus Server; the default is " + serverPort, s => serverPort = int.Parse(s));
            Options.Add("web-socket=", "When using active communication over websockets, the address of the Octopus Server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => webSocket = s);
        }

        protected override void Start()
        {
            var distinctTrustedThumbprints = tentacleConfiguration.Value.TrustedOctopusThumbprints.Distinct().ToArray();
            if (!distinctTrustedThumbprints.Any())
                throw new ControlledFailureException("Before server communications can be modified, trust must be established with the configure command");

            base.Start();
            if (string.IsNullOrWhiteSpace(serverThumbprint))
            {
                if (distinctTrustedThumbprints.Count() != 1)
                    throw new ControlledFailureException("More than one server is trusted; please provide the thumbprint of the server to configure, e.g. --thumbprint=...");

                serverThumbprint = distinctTrustedThumbprints.Single();
            }

            CommunicationStyle communicationStyle;
            if (!Enum.TryParse(style, true, out communicationStyle))
                throw new ControlledFailureException("Please specify a valid communications style, e.g. --style=TentaclePassive");

            var servers = tentacleConfiguration.Value.TrustedOctopusServers.Where(s => s.Thumbprint == serverThumbprint).ToArray();
            if (servers.None())
                throw new ControlledFailureException("No trusted server was found with the supplied thumbprint");


            if (communicationStyle == CommunicationStyle.TentacleActive)
                SetupActive(servers);
            else
                SetupPassive(servers);

            VoteForRestart();

            log.Info("Updated server communications configuration");
        }

        void SetupPassive(OctopusServerConfiguration[] servers)
        {
            var server = servers.FirstOrDefault(s => s.Address == null)
                ?? new OctopusServerConfiguration(serverThumbprint);
            server.CommunicationStyle = CommunicationStyle.TentaclePassive;
            tentacleConfiguration.Value.AddOrUpdateTrustedOctopusServer(server);
        }

        void SetupActive(OctopusServerConfiguration[] servers)
        {
            var address = GetActiveAddress();

            var server = servers.FirstOrDefault(s => s.Address == address)
                ?? servers.FirstOrDefault(s => s.CommunicationStyle == CommunicationStyle.None)
                ?? new OctopusServerConfiguration(serverThumbprint);

            server.Address = address;
            server.CommunicationStyle = CommunicationStyle.TentacleActive;

            if (server.SubscriptionId == null)
            {
                var existingPollingConfiguration = servers.FirstOrDefault(s =>
                    (s.CommunicationStyle == CommunicationStyle.TentacleActive || (s.CommunicationStyle == CommunicationStyle.KubernetesTentacle && s.KubernetesTentacleCommunicationMode == TentacleCommunicationModeResource.Polling)) && s.SubscriptionId != null);
                server.SubscriptionId = existingPollingConfiguration?.SubscriptionId ?? new Uri($"poll://{RandomStringGenerator.Generate(20).ToLowerInvariant()}/").ToString();
            }

            tentacleConfiguration.Value.AddOrUpdateTrustedOctopusServer(server);
        }

        Uri GetActiveAddress()
        {
            var hasHost = !string.IsNullOrWhiteSpace(serverHost);
            var hasWebSocket = !string.IsNullOrWhiteSpace(webSocket);
            if (!hasHost && !hasWebSocket)
                throw new ControlledFailureException("Please provide either the server hostname or websocket address, e.g. --host=OCTOPUS");

            if (hasHost && hasWebSocket)
                throw new ControlledFailureException("The hostname and websocket options cannot be used together");

            if (hasHost)
                return new Uri($"https://{serverHost}:{serverPort}");

            var address = new Uri(webSocket);

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