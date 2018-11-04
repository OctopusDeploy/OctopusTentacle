﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Halibut;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Properties;

namespace Octopus.Tentacle.Commands
{
    public abstract class RegisterMachineCommandBase<TRegistrationOperationType> : AbstractStandardCommand where TRegistrationOperationType : IRegisterMachineOperationBase
    {
        readonly Lazy<TRegistrationOperationType> lazyRegisterMachineOperation;
        readonly Lazy<ITentacleConfiguration> configuration;
        readonly Lazy<IOctopusServerChecker> octopusServerChecker;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;

        readonly ILog log;
        readonly ApiEndpointOptions api;
        string name;
        string policy;
        string publicName;
        bool allowOverwrite;
        string comms = "TentaclePassive";
        int serverCommsPort = 10943;
        string proxy;
        string spaceName;
        string serverWebSocketAddress;
        int? tentacleCommsPort = null;

        public RegisterMachineCommandBase(Lazy<TRegistrationOperationType> lazyRegisterMachineOperation,
            Lazy<ITentacleConfiguration> configuration,
            ILog log,
            IApplicationInstanceSelector selector,
            Lazy<IOctopusServerChecker> octopusServerChecker,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISpaceRepositoryFactory spaceRepositoryFactory)
            : base(selector)
        {
            this.lazyRegisterMachineOperation = lazyRegisterMachineOperation;
            this.configuration = configuration;
            this.log = log;
            this.octopusServerChecker = octopusServerChecker;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.spaceRepositoryFactory = spaceRepositoryFactory;

            api = AddOptionSet(new ApiEndpointOptions(Options));

            Options.Add("name=", "Name of the machine when registered; the default is the hostname", s => name = s);
            Options.Add("policy=", "The name of a machine policy that applies to this machine", s => policy = s);
            Options.Add("h|publicHostName=", "An Octopus-accessible DNS name/IP address for this machine; the default is the hostname", s => publicName = s);
            Options.Add("f|force", "Allow overwriting of existing machines", s => allowOverwrite = true);
            Options.Add("comms-style=", "The communication style to use - either TentacleActive or TentaclePassive; the default is " + comms, s => comms = s);
            Options.Add("proxy=", "When using passive communication, the name of a proxy that Octopus should connect to the Tentacle through - e.g., 'Proxy ABC' where the proxy name is already configured in Octopus; the default is to connect to the machine directly", s => proxy = s);
            Options.Add("space=", "The space which this machine will be added to, - e.g. 'Default' where Default is the name of an existing space; the default is the default space", s => spaceName = s);
            Options.Add("server-comms-port=", "When using active communication, the comms port on the Octopus Server; the default is " + serverCommsPort, s => serverCommsPort = int.Parse(s));
            Options.Add("server-web-socket=", "When using active communication over websockets, the address of the Octopus Server, eg 'wss://example.com/OctopusComms'. Refer to http://g.octopushq.com/WebSocketComms", s => serverWebSocketAddress = s);
            Options.Add("tentacle-comms-port=", "When using passive communication, the comms port that the Octopus Server is instructed to call back on to reach this machine; defaults to the configured listening port", s => tentacleCommsPort = int.Parse(s));
        }

        protected override void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            CheckArgs();

            CommunicationStyle communicationStyle;
            if (!Enum.TryParse(comms, true, out communicationStyle))
                throw new ControlledFailureException("Please specify a valid communications style, e.g. --comms-style=TentaclePassive");

            if (configuration.Value.TentacleCertificate == null)
                throw new ControlledFailureException("No certificate has been generated for this Tentacle. Please run the new-certificate command first.");

            if(communicationStyle == CommunicationStyle.TentacleActive && !string.IsNullOrWhiteSpace(proxy))
                throw new ControlledFailureException("Option --proxy can only be used with --comms-style=TentaclePassive.  To set a proxy for a polling Tentacle use the polling-proxy command first and then register the Tentacle with register-with.");

            Uri serverAddress = null;

            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            IWebProxy proxyOverride = null;
            string sslThumbprint = null;
            if (communicationStyle == CommunicationStyle.TentacleActive)
            {
                serverAddress = GetActiveTentacleAddress();
                proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);
                sslThumbprint = octopusServerChecker.Value.CheckServerCommunicationsIsOpen(serverAddress, proxyOverride);
            }

            log.Info($"Registering the tentacle with the server at {api.ServerUri}");

            using (var client = await octopusClientInitializer.CreateClient(api, proxyOverride))
            {
                var spaceRepository = await spaceRepositoryFactory.CreateSpaceRepository(client, spaceName);
                await RegisterMachine(client.ForSystem(), spaceRepository, serverAddress, sslThumbprint, communicationStyle);
            }
        }

        async Task RegisterMachine(IOctopusSystemAsyncRepository systemRepository, IOctopusSpaceAsyncRepository repository, Uri serverAddress, string sslThumbprint, CommunicationStyle communicationStyle)
        {
            await ConfirmTentacleCanRegisterWithServerBasedOnItsVersion(systemRepository);

            var server = new OctopusServerConfiguration(await GetServerThumbprint(systemRepository, serverAddress, sslThumbprint))
            {
                Address = serverAddress,
                CommunicationStyle = communicationStyle
            };

            var registerMachineOperation = lazyRegisterMachineOperation.Value;
            registerMachineOperation.MachineName = string.IsNullOrWhiteSpace(name) ? Environment.MachineName : name;

            var existingServer = configuration.Value.TrustedOctopusServers.FirstOrDefault(x => x.Address == server.Address && x.CommunicationStyle == communicationStyle);
            if (communicationStyle == CommunicationStyle.TentaclePassive)
            {
                registerMachineOperation.TentacleHostname = string.IsNullOrWhiteSpace(publicName) ? Environment.MachineName : publicName;
                registerMachineOperation.TentaclePort = tentacleCommsPort ?? configuration.Value.ServicesPortNumber;
                registerMachineOperation.ProxyName = proxy;
            }
            else if (communicationStyle == CommunicationStyle.TentacleActive)
            {
                Uri subscriptionId;
                if (existingServer?.SubscriptionId != null)
                    subscriptionId = new Uri(existingServer.SubscriptionId);
                else
                    subscriptionId = new Uri("poll://" + RandomStringGenerator.Generate(20).ToLowerInvariant() + "/");
                registerMachineOperation.SubscriptionId = subscriptionId;
                server.SubscriptionId = subscriptionId.ToString();
            }

            registerMachineOperation.MachinePolicy = policy;
            registerMachineOperation.AllowOverwrite = allowOverwrite;
            registerMachineOperation.CommunicationStyle = communicationStyle;
            registerMachineOperation.TentacleThumbprint = configuration.Value.TentacleCertificate.Thumbprint;

            EnhanceOperation(repository, registerMachineOperation);

            await registerMachineOperation.ExecuteAsync(repository);

            configuration.Value.AddOrUpdateTrustedOctopusServer(server);
            VoteForRestart();

            log.Info("Machine registered successfully");
        }

        protected abstract void CheckArgs();
        protected abstract void EnhanceOperation(IOctopusSpaceAsyncRepository repository, TRegistrationOperationType registerOperation);

        async Task<string> GetServerThumbprint(IOctopusSystemAsyncRepository repository, Uri serverAddress, string sslThumbprint)
        {
            if (serverAddress != null && ServiceEndPoint.IsWebSocketAddress(serverAddress))
            {
                if (sslThumbprint == null)
                    throw new Exception($"Could not determine thumbprint of the SSL Certificate at {serverAddress}");
                return sslThumbprint;
            }
            var certificate = await repository.CertificateConfiguration.GetOctopusCertificate();
            return certificate.Thumbprint;
        }

        Uri GetActiveTentacleAddress()
        {
            if (string.IsNullOrWhiteSpace(serverWebSocketAddress))
                return new Uri($"https://{api.ServerUri.Host}:{serverCommsPort}");

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

        #region Helpers

        async Task ConfirmTentacleCanRegisterWithServerBasedOnItsVersion(IOctopusSystemAsyncRepository repository)
        {
            var rootDocument = await repository.LoadRootDocument();
            // Eg. Check they're not trying to register a 3.* Tentacle with a 2.* API Server.
            if (string.IsNullOrEmpty(rootDocument.Version))
                throw new ControlledFailureException("Unable to determine the Octopus Server version.");

            var serverVersion = SemanticVersion.Parse(rootDocument.Version);
            var tentacleVersion = SemanticVersion.Parse(OctopusTentacle.SemanticVersionInfo.MajorMinorPatch);
            if (serverVersion.Version.Major == 0 || tentacleVersion.Version.Major == 0)
                return;

            if (serverVersion.Version.Major < 3)
                throw new ControlledFailureException($"You cannot register a {tentacleVersion.Version.Major}.* Octopus Tentacle with a {serverVersion.Version.Major}.* Octopus Server.");
        }

        #endregion

    }
}