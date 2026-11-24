using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
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

            var output = new ShowConfigurationOutput();
            await CollectConfigurationSettings(output);
            var json = output.ToJson();

            if (!string.IsNullOrWhiteSpace(file))
            {
                WriteToFile(json);
            }
            else
            {
                Console.WriteLine(json);
            }
        }

        void WriteToFile(string json)
        {
            var parentDirectory = Path.GetDirectoryName(file) ?? throw new Exception("Configuration file location must include directory information");
            fileSystem.EnsureDirectoryExists(parentDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(file, 1024 * 1024);
            using var writer = new StreamWriter(fileSystem.OpenFile(file, FileMode.Create));
            writer.Write(json);
        }

        async Task CollectConfigurationSettings(ShowConfigurationOutput output)
        {
            output.AddRange(homeConfiguration.Value.DisplaySettings());

            output.AddRange(tentacleConfiguration.Value.DisplaySettings(
                excluding: new [] {
                    TentacleConfiguration.CertificateSettingName,
                    TentacleConfiguration.LastReceivedHandshakeSettingName,
                    TentacleConfiguration.IsRegisteredSettingName
                }));

            output.AddRange(watchdog.Value.GetConfiguration().DisplaySettings());

            //advanced settings
            if (apiEndpointOptions.IsSupplied)
            {
                await CollectServerSideConfiguration(output);
            }
        }

        async Task CollectServerSideConfiguration(ShowConfigurationOutput output)
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
                            await CollectionServerSideConfigurationFromMachine(output, repository, machine);
                            break;

                        case 1 when matches.First() is WorkerResource worker:
                            await CollectionServerSideConfigurationFromWorker(output, repository, worker);
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

        async Task CollectionServerSideConfigurationFromMachine(ShowConfigurationOutput output, IOctopusSpaceAsyncRepository repository, MachineResource machine)
        {
            var environments = await repository.Environments.FindAll(CancellationToken.None);
            output.Add("Tentacle.Environments", environments.Where(x => machine.EnvironmentIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

            if (await repository.HasLink("Tenants"))
            {
                var tenants = await repository.Tenants.FindAll(CancellationToken.None);
                output.Add("Tentacle.Tenants", tenants.Where(x => machine.TenantIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                output.Add("Tentacle.TenantTags", machine.TenantTags);
            }

            output.Add("Tentacle.Roles", machine.Roles);
            if (machine.MachinePolicyId != null)
            {
                var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId, CancellationToken.None);
                output.Add("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
            }

            output.Add("Tentacle.DisplayName", machine.Name);
            if (machine.Endpoint is ListeningTentacleEndpointResource listening)
            {
                output.Add("Tentacle.Communication.PublicHostName", listening.Uri);
            }
        }

        async Task CollectionServerSideConfigurationFromWorker(ShowConfigurationOutput output, IOctopusSpaceAsyncRepository repository, WorkerResource machine)
        {
            var workerPools = await repository.WorkerPools.FindAll(CancellationToken.None);
            output.Add("Tentacle.WorkerPools", workerPools.Where(x => machine.WorkerPoolIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

            if (machine.MachinePolicyId != null)
            {
                var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId, CancellationToken.None);
                output.Add("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
            }

            output.Add("Tentacle.DisplayName", machine.Name);
            if (machine.Endpoint is ListeningTentacleEndpointResource listening)
            {
                output.Add("Tentacle.Communication.PublicHostName", listening.Uri);
            }
        }
    }
}
