﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security;
using Octopus.Shared.Services;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class ShowConfigurationCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        readonly IOctopusFileSystem fileSystem;
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly Lazy<IWatchdog> watchdog;
        string file;
        readonly ApiEndpointOptions apiEndpointOptions;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISystemLog log;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;
        string spaceName;

        public override bool SuppressConsoleLogging => true;

        public ShowConfigurationCommand(
            IApplicationInstanceSelector instanceSelector,
            IOctopusFileSystem fileSystem,
            Lazy<ITentacleConfiguration> tentacleConfiguration,
            Lazy<IWatchdog> watchdog,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISystemLog log,
            ISpaceRepositoryFactory spaceRepositoryFactory) : base(instanceSelector, log)
        {
            this.instanceSelector = instanceSelector;
            this.fileSystem = fileSystem;
            this.tentacleConfiguration = tentacleConfiguration;
            this.watchdog = watchdog;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;
            this.log = log;
            this.spaceRepositoryFactory = spaceRepositoryFactory;

            Options.Add("file=", "Exports the server configuration to a file. If not specified output goes to the console", v => file = v);
            Options.Add("space=", "The space from which the server data configuration will be retrieved for, - e.g. 'Finance Department' where Finance Department is the name of an existing space; the default value is the Default space, if one is designated.", s => spaceName = s);
            apiEndpointOptions = AddOptionSet(new ApiEndpointOptions(Options) { Optional = true });
        }

        protected override void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        async Task StartAsync()
        {
            base.Start();

            DictionaryKeyValueStore outputFile;

            if (!string.IsNullOrWhiteSpace(file))
            {
                outputFile = new JsonHierarchicalFileKeyValueStore(file, fileSystem, autoSaveOnSet: false, isWriteOnly: true);
            }
            else
            {
                outputFile = new JsonHierarchicalConsoleKeyValueStore();
            }

            await CollectConfigurationSettings(outputFile);

            outputFile.Save();
        }

        internal async Task CollectConfigurationSettings(DictionaryKeyValueStore outputStore)
        {
            var configStore = instanceSelector.GetCurrentConfiguration();

            var oldHomeConfiguration = new HomeConfiguration(ApplicationName.Tentacle, configStore);
            var homeConfiguration = new WritableHomeConfiguration(ApplicationName.Tentacle, outputStore);
            homeConfiguration.SetHomeDirectory(oldHomeConfiguration.HomeDirectory);

            var certificateGenerator = new CertificateGenerator();
            var newTentacleConfiguration = new WritableTentacleConfiguration(outputStore, homeConfiguration, certificateGenerator, tentacleConfiguration.Value.ProxyConfiguration, tentacleConfiguration.Value.PollingProxyConfiguration, new NullLog());

            newTentacleConfiguration.SetApplicationDirectory(tentacleConfiguration.Value.ApplicationDirectory);
            newTentacleConfiguration.SetListenIpAddress(tentacleConfiguration.Value.ListenIpAddress);
            newTentacleConfiguration.SetNoListen(tentacleConfiguration.Value.NoListen);
            newTentacleConfiguration.SetServicesPortNumber(tentacleConfiguration.Value.ServicesPortNumber);
            foreach (var octopusServerConfiguration in tentacleConfiguration.Value.TrustedOctopusServers)
            {
                newTentacleConfiguration.AddOrUpdateTrustedOctopusServer(octopusServerConfiguration);
            }

            //we dont want the actual certificate, as its encrypted, and we get a different output everytime
            outputStore.Set<string>("Tentacle.CertificateThumbprint", tentacleConfiguration.Value.TentacleCertificate?.Thumbprint);

            var watchdogConfiguration = watchdog.Value.GetConfiguration();
            watchdogConfiguration.WriteTo(outputStore);

            //advanced settings
            if (apiEndpointOptions.IsSupplied)
            {
                await CollectServerSideConfiguration(outputStore);
            }
        }

        async Task CollectServerSideConfiguration(IWritableKeyValueStore outputStore)
        {
            var proxyOverride = proxyConfig.ParseToWebProxy(tentacleConfiguration.Value.PollingProxyConfiguration);
            try
            {
                using (var client = await octopusClientInitializer.CreateClient(apiEndpointOptions, proxyOverride))
                {
                    var repository = await spaceRepositoryFactory.CreateSpaceRepository(client, spaceName);
                    var matchingMachines = await repository.Machines.FindByThumbprint(tentacleConfiguration.Value.TentacleCertificate.Thumbprint);

                    switch (matchingMachines.Count)
                    {
                        case 0:
                            log.Error($"No machines were found on the specified server with the thumbprint '{tentacleConfiguration.Value.TentacleCertificate.Thumbprint}'. Unable to retrieve server side configuration.");
                            break;

                        case 1:
                            await CollectionServerSideConfigurationFromMachine(outputStore, repository, matchingMachines.First());
                            break;

                        default:
                            if (matchingMachines.Count > 1)
                                throw new ControlledFailureException("This Tentacle is registered multiple times with the server - unable to display configuration");
                            break;
                    }
                }
            }
            catch (OctopusResourceNotFoundException ex)
            {
                log.Warn(ex, $"Error contacting server '{apiEndpointOptions.Server}'.");
                throw new ControlledFailureException($"The specified server '{apiEndpointOptions.Server}' did not appear to be an Octopus Server. Check the server URL and try again.", ex);
            }
            catch (OctopusSecurityException ex)
            {
                log.Warn(ex, $"Error authenticationg with server '{apiEndpointOptions.Server}'.");
                throw new ControlledFailureException(ex.Message, ex);
            }
        }

        async Task CollectionServerSideConfigurationFromMachine(IWritableKeyValueStore outputStore, IOctopusSpaceAsyncRepository repository, MachineResource machine)
        {
            var environments = await repository.Environments.FindAll();
            outputStore.Set("Tentacle.Environments", environments.Where(x => machine.EnvironmentIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

            if (await repository.HasLink("Tenants"))
            {
                var tenants = await repository.Tenants.FindAll();
                outputStore.Set("Tentacle.Tenants", tenants.Where(x => machine.TenantIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                outputStore.Set("Tentacle.TenantTags", machine.TenantTags);
            }

            outputStore.Set("Tentacle.Roles", machine.Roles);
            if (machine.MachinePolicyId != null)
            {
                var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId);
                outputStore.Set("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
            }

            outputStore.Set<string>("Tentacle.DisplayName", machine.Name);
            if (machine.Endpoint is ListeningTentacleEndpointResource)
                outputStore.Set<string>("Tentacle.Communication.PublicHostName", ((ListeningTentacleEndpointResource)machine.Endpoint).Uri);
        }
    }
}
