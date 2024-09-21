using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Watchdog;

namespace Octopus.Tentacle.Commands
{
    public class ShowConfigurationCommand : AbstractStandardCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly Lazy<IHomeConfiguration> homeConfiguration;
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly Lazy<IWatchdog> watchdog;
        string file = null!;
        readonly ApiEndpointOptions apiEndpointOptions;
        readonly IProxyConfigParser proxyConfig;
        readonly IOctopusClientInitializer octopusClientInitializer;
        readonly ISystemLog log;
        readonly ISpaceRepositoryFactory spaceRepositoryFactory;
        string spaceName = null!;

        public override bool SuppressConsoleLogging => true;

        public ShowConfigurationCommand(
            IApplicationInstanceSelector instanceSelector,
            IOctopusFileSystem fileSystem,
            Lazy<IHomeConfiguration> homeConfiguration,
            Lazy<ITentacleConfiguration> tentacleConfiguration,
            Lazy<IWatchdog> watchdog,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer,
            ISystemLog log,
            ISpaceRepositoryFactory spaceRepositoryFactory,
            ILogFileOnlyLogger logFileOnlyLogger) : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.fileSystem = fileSystem;
            this.homeConfiguration = homeConfiguration;
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
            log.Info("Inside ShowConfigurationCommand Start().");

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
            log.Info("Finished ShowConfigurationCommand Start().");
        }

        async Task CollectConfigurationSettings(DictionaryKeyValueStore outputStore)
        {
            homeConfiguration.Value.WriteTo(outputStore);

            tentacleConfiguration.Value.WriteTo(outputStore,
                excluding: new [] {
                    TentacleConfiguration.CertificateSettingName,
                    TentacleConfiguration.LastReceivedHandshakeSettingName,
                    TentacleConfiguration.IsRegisteredSettingName
                });

            watchdog.Value.GetConfiguration().WriteTo(outputStore);

            //advanced settings
            if (apiEndpointOptions.IsSupplied)
            {
                await CollectServerSideConfiguration(outputStore);
            }
        }

        async Task CollectServerSideConfiguration(IWritableKeyValueStore outputStore)
        {
            if (tentacleConfiguration.Value.TentacleCertificate == null)
                throw new ControlledFailureException("No certificate has been generated for this Tentacle. Unable to display configuration.");

            var proxyOverride = proxyConfig.ParseToWebProxy(tentacleConfiguration.Value.PollingProxyConfiguration);
            try
            {
                using (var client = await octopusClientInitializer.CreateClient(apiEndpointOptions, proxyOverride))
                {
                    var repository = await spaceRepositoryFactory.CreateSpaceRepository(client, spaceName);
                    var matchingMachines = (await repository.Machines.FindByThumbprint(tentacleConfiguration.Value.TentacleCertificate.Thumbprint)).Cast<MachineBasedResource>().ToArray();
                    var matchingWorkers = (await repository.Workers.FindByThumbprint(tentacleConfiguration.Value.TentacleCertificate.Thumbprint)).Cast<MachineBasedResource>().ToArray();

                    var matches = matchingMachines.Concat(matchingWorkers).ToList();
                    switch (matches.Count)
                    {
                        case 0:
                            log.Error($"No machines or workers were found on the specified server with the thumbprint '{tentacleConfiguration.Value.TentacleCertificate.Thumbprint}'. Unable to retrieve server side configuration.");
                            break;

                        case 1 when matches.First() is MachineResource machine:
                            await CollectionServerSideConfigurationFromMachine(outputStore, repository, machine);
                            break;

                        case 1 when matches.First() is WorkerResource worker:
                            await CollectionServerSideConfigurationFromWorker(outputStore, repository, worker);
                            break;

                        default:
                            throw new ControlledFailureException("This Tentacle is registered multiple times with the server. Unable to display configuration.");
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
            var environments = await repository.Environments.FindAll(CancellationToken.None);
            outputStore.Set("Tentacle.Environments", environments.Where(x => machine.EnvironmentIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

            if (await repository.HasLink("Tenants"))
            {
                var tenants = await repository.Tenants.FindAll(CancellationToken.None);
                outputStore.Set("Tentacle.Tenants", tenants.Where(x => machine.TenantIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                outputStore.Set("Tentacle.TenantTags", machine.TenantTags);
            }

            outputStore.Set("Tentacle.Roles", machine.Roles);
            if (machine.MachinePolicyId != null)
            {
                var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId, CancellationToken.None);
                outputStore.Set("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
            }

            outputStore.Set<string>("Tentacle.DisplayName", machine.Name);
            if (machine.Endpoint is ListeningTentacleEndpointResource)
                outputStore.Set<string>("Tentacle.Communication.PublicHostName", ((ListeningTentacleEndpointResource)machine.Endpoint).Uri);
        }

        async Task CollectionServerSideConfigurationFromWorker(IWritableKeyValueStore outputStore, IOctopusSpaceAsyncRepository repository, WorkerResource machine)
        {
            var workerPools = await repository.WorkerPools.FindAll(CancellationToken.None);
            outputStore.Set("Tentacle.WorkerPools", workerPools.Where(x => machine.WorkerPoolIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

            if (machine.MachinePolicyId != null)
            {
                var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId, CancellationToken.None);
                outputStore.Set("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
            }

            outputStore.Set<string>("Tentacle.DisplayName", machine.Name);
            if (machine.Endpoint is ListeningTentacleEndpointResource)
                outputStore.Set<string>("Tentacle.Communication.PublicHostName", ((ListeningTentacleEndpointResource)machine.Endpoint).Uri);
        }
    }
}
