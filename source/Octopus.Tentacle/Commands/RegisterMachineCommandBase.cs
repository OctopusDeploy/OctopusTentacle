using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Properties;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public abstract class RegisterMachineCommandBase<TRegistrationOperationType> : AbstractStandardCommand where TRegistrationOperationType : IRegisterMachineOperationBase
    {
        readonly Lazy<TRegistrationOperationType> lazyRegisterMachineOperation;
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        readonly Lazy<IOctopusServerChecker> octopusServerChecker;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;

        readonly ISystemLog log;
        readonly ApiEndpointOptions api;
        readonly TentacleOptions tentacleOptions;

        public RegisterMachineCommandBase(Lazy<TRegistrationOperationType> lazyRegisterMachineOperation,
            Lazy<IWritableTentacleConfiguration> configuration,
            ISystemLog log,
            IApplicationInstanceSelector selector,
            Lazy<IOctopusServerChecker> octopusServerChecker,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISpaceRepositoryFactory spaceRepositoryFactory,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(selector, log, logFileOnlyLogger)
        {
            this.lazyRegisterMachineOperation = lazyRegisterMachineOperation;
            this.configuration = configuration;
            this.log = log;
            this.octopusServerChecker = octopusServerChecker;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.spaceRepositoryFactory = spaceRepositoryFactory;

            api = AddOptionSet(new ApiEndpointOptions(Options));
            tentacleOptions = AddOptionSet(new TentacleOptions(Options));
        }

        protected override void Start()
        {
            base.Start();
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            CheckArgs();

            if (configuration.Value.TentacleCertificate == null)
                throw new ControlledFailureException("No certificate has been generated for this Tentacle. Please run the new-certificate command first.");

            Uri? serverAddress = null;

            var isPolling = IsPolling(tentacleOptions.CommunicationStyle);

            var useDefaultProxy = isPolling
                ? configuration.Value.PollingProxyConfiguration.UseDefaultProxy
                : configuration.Value.ProxyConfiguration.UseDefaultProxy;

            //if we are on a polling tentacle with a polling proxy set up, use the api through that proxy
            IWebProxy? proxyOverride = null;
            string? sslThumbprint = null;
            if (isPolling)
            {
                serverAddress = GetActiveTentacleAddress();
                proxyOverride = proxyConfig.ParseToWebProxy(configuration.Value.PollingProxyConfiguration);
                sslThumbprint = octopusServerChecker.Value.CheckServerCommunicationsIsOpen(serverAddress, proxyOverride);
            }

            log.Info($"Registering the tentacle with the server at {api.ServerUri}");

            using var client = proxyOverride == null
                ? await octopusClientInitializer.CreateClient(api, useDefaultProxy)
                : await octopusClientInitializer.CreateClient(api, proxyOverride);

            var spaceRepository = await spaceRepositoryFactory.CreateSpaceRepository(client, tentacleOptions.SpaceName);
            await RegisterMachine(client.ForSystem(), spaceRepository, serverAddress, sslThumbprint, tentacleOptions.CommunicationStyle);
        }

        bool IsPolling(CommunicationStyle communicationStyle)
        {
            return communicationStyle == CommunicationStyle.TentacleActive;
        }

        async Task RegisterMachine(IOctopusSystemAsyncRepository systemRepository, IOctopusSpaceAsyncRepository repository, Uri? serverAddress, string? sslThumbprint, CommunicationStyle communicationStyle)
        {
            await ConfirmTentacleCanRegisterWithServerBasedOnItsVersion(systemRepository);

            var server = new OctopusServerConfiguration(await GetServerThumbprint(systemRepository, serverAddress, sslThumbprint))
            {
                Address = serverAddress!,
                CommunicationStyle = communicationStyle
            };

            var registerMachineOperation = lazyRegisterMachineOperation.Value;
            registerMachineOperation.MachineName = string.IsNullOrWhiteSpace(tentacleOptions.Name) ? Environment.MachineName : tentacleOptions.Name;

            var existingServer = configuration.Value.TrustedOctopusServers.FirstOrDefault(x => x.Address == server.Address && x.CommunicationStyle == communicationStyle);
            if (communicationStyle == CommunicationStyle.TentaclePassive)
            {
                registerMachineOperation.TentacleHostname = string.IsNullOrWhiteSpace(tentacleOptions.PublicName) ? Environment.MachineName : tentacleOptions.PublicName;
                registerMachineOperation.TentaclePort = tentacleOptions.TentacleCommsPort ?? configuration.Value.ServicesPortNumber;
                registerMachineOperation.ProxyName = tentacleOptions.Proxy;
            }
            else if (communicationStyle == CommunicationStyle.TentacleActive)
            {
                Uri subscriptionId;
                if (existingServer?.SubscriptionId != null)
                    subscriptionId = new Uri(existingServer.SubscriptionId);
                else
                    subscriptionId = PollingSubscriptionId.Generate();
                registerMachineOperation.SubscriptionId = subscriptionId;
                server.SubscriptionId = subscriptionId.ToString();
            }

            registerMachineOperation.MachinePolicy = tentacleOptions.Policy;
            registerMachineOperation.AllowOverwrite = tentacleOptions.AllowOverwrite;
            registerMachineOperation.CommunicationStyle = communicationStyle;
            registerMachineOperation.TentacleThumbprint = configuration.Value.TentacleCertificate!.Thumbprint;

            EnhanceOperation(registerMachineOperation);

            try
            {
                await registerMachineOperation.ExecuteAsync(repository);
            }
            catch (InvalidRegistrationArgumentsException ex)
            {
                throw new ControlledFailureException(ex.Message, ex);
            }
            catch (OctopusValidationException ex)
            {
                throw new ControlledFailureException(ex.Message, ex);
            }

            configuration.Value.AddOrUpdateTrustedOctopusServer(server);
            VoteForRestart();

            log.Info("Machine registered successfully");
        }

        protected abstract void CheckArgs();

        protected abstract void EnhanceOperation(TRegistrationOperationType registerOperation);

        async Task<string> GetServerThumbprint(IOctopusSystemAsyncRepository repository, Uri? serverAddress, string? sslThumbprint)
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
            if (string.IsNullOrWhiteSpace(tentacleOptions.ServerWebSocketAddress))
            {
                Uri serverCommsAddressUri;
                int serverCommsPort;

                if (string.IsNullOrEmpty(tentacleOptions.ServerCommsAddress))
                {
                    serverCommsAddressUri = api.ServerUri;
                    serverCommsPort = TentacleOptions.DefaultServerCommsPort;
                }
                else
                {
                    serverCommsAddressUri = new Uri(tentacleOptions.ServerCommsAddress);
                    serverCommsPort = serverCommsAddressUri.Port;
                }

                return new Uri($"https://{serverCommsAddressUri.Host}:{serverCommsPort}");
            }

            if (!HalibutRuntime.OSSupportsWebSockets)
                throw new ControlledFailureException("Websockets is only supported on Windows Server 2012 and later");

            var address = new Uri(tentacleOptions.ServerWebSocketAddress);

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
            var rootDocument = await repository.LoadRootDocument(CancellationToken.None);
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