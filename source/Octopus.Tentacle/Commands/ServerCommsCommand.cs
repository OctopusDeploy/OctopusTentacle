﻿using System;
using System.Linq;
using Halibut;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ServerCommsCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ILog log;
        string serverThumbprint;
        string style;
        string serverHost;
        int serverPort = 10943;
        string webSocket;

        public ServerCommsCommand(Lazy<ITentacleConfiguration> tentacleConfiguration, ILog log, IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("thumbprint=", "The thumbprint of the Octopus server to configure communication with; if only one Octopus server is configured, this may be omitted", s => serverThumbprint = s);
            Options.Add("style=", "The communication style to use with the Octopus server - either TentacleActive or TentaclePassive", s => style = s);
            Options.Add("host=", "When using active communication, the host name of the Octopus server", s => serverHost = s);
            Options.Add("port=", "When using active communication, the communications port of the Octopus server; the default is " + serverPort, s => serverPort = int.Parse(s));
            Options.Add("web-socket=", "When using active communication over websockets, the address of the Octopus server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => webSocket = s);
        }

        protected override void Start()
        {
            if (!tentacleConfiguration.Value.TrustedOctopusThumbprints.Any())
                throw new ControlledFailureException("Before server communications can be modified, trust must be established with the configure command");

            if (string.IsNullOrWhiteSpace(serverThumbprint))
            {
                if (tentacleConfiguration.Value.TrustedOctopusThumbprints.Count() != 1)
                    throw new ControlledFailureException("More than one server is trusted; please provide the thumbprint of the server to configure, e.g. --thumbprint=...");

                serverThumbprint = tentacleConfiguration.Value.TrustedOctopusThumbprints.Single();
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
                server.SubscriptionId = new Uri($"poll://{RandomStringGenerator.Generate(20).ToLowerInvariant()}/").ToString();

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