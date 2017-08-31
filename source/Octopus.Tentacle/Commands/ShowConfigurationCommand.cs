using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model.Endpoints;
using Octopus.Shared;
using Octopus.Shared.Configuration;
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

        public override bool SuppressConsoleLogging => true;

        public ShowConfigurationCommand(
            IApplicationInstanceSelector instanceSelector,
            IOctopusFileSystem fileSystem,
            Lazy<ITentacleConfiguration> tentacleConfiguration,
            Lazy<IWatchdog> watchdog,
            IProxyConfigParser proxyConfig,
            IOctopusClientInitializer octopusClientInitializer) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            this.fileSystem = fileSystem;
            this.tentacleConfiguration = tentacleConfiguration;
            this.watchdog = watchdog;
            this.proxyConfig = proxyConfig;
            this.octopusClientInitializer = octopusClientInitializer;

            Options.Add("file=", "Exports the server configuration to a file. If not specified output goes to the console", v => file = v);
            apiEndpointOptions = AddOptionSet(new ApiEndpointOptions(Options) {Optional = true});
        }

        protected override void Start()
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

            CollectConfigurationSettings(outputFile).GetAwaiter().GetResult();

            outputFile.Save();
        }

        async Task CollectConfigurationSettings(DictionaryKeyValueStore outputStore)
        {
            var configStore = new XmlFileKeyValueStore(instanceSelector.GetCurrentInstance().ConfigurationPath);

            var oldHomeConfiguration = new HomeConfiguration(ApplicationName.Tentacle, configStore);
            var homeConfiguration = new HomeConfiguration(ApplicationName.Tentacle, outputStore)
            {
                HomeDirectory = oldHomeConfiguration.HomeDirectory
            };

            var certificateGenerator = new CertificateGenerator();
            var newTentacleConfiguration = new TentacleConfiguration(outputStore, homeConfiguration, certificateGenerator, tentacleConfiguration.Value.ProxyConfiguration, tentacleConfiguration.Value.PollingProxyConfiguration, new NullLog())
            {
                ApplicationDirectory = tentacleConfiguration.Value.ApplicationDirectory,
                ListenIpAddress = tentacleConfiguration.Value.ListenIpAddress,
                NoListen = tentacleConfiguration.Value.NoListen,
                ServicesPortNumber = tentacleConfiguration.Value.ServicesPortNumber,
                TrustedOctopusServers = tentacleConfiguration.Value.TrustedOctopusServers
            };

            //we dont want the actual certificate, as its encrypted, and we get a different output everytime
            outputStore.Set<string>("Tentacle.CertificateThumbprint", tentacleConfiguration.Value.TentacleCertificate.Thumbprint);

            var watchdogConfiguration = watchdog.Value.GetConfiguration();
            watchdogConfiguration.WriteTo(outputStore);

            //advanced settings
            if (apiEndpointOptions.IsSupplied)
            {
                var proxyOverride = proxyConfig.ParseToWebProxy(tentacleConfiguration.Value.PollingProxyConfiguration);
                using (var client = await octopusClientInitializer.CreateClient(apiEndpointOptions, proxyOverride))
                {
                    var repository = new OctopusAsyncRepository(client);
                    var matchingMachines = await repository.Machines.FindByThumbprint(tentacleConfiguration.Value.TentacleCertificate.Thumbprint);

                    switch (matchingMachines.Count)
                    {
                        case 0:
                            Log.Error($"No machines were found on the specified server with the thumbprint '{tentacleConfiguration.Value.TentacleCertificate.Thumbprint}'. Unable to retrieve server side configuration.");
                            break;

                        case 1:
                            var machine = matchingMachines.First();
                            var environments = await repository.Environments.FindAll();
                            outputStore.Set("Tentacle.Environments", environments.Where(x => machine.EnvironmentIds.Contains(x.Id)).Select(x => new {x.Id, x.Name}));
                            var featuresConfiguration = await repository.FeaturesConfiguration.GetFeaturesConfiguration();
                            if (featuresConfiguration.IsMultiTenancyEnabled)
                            {
                                var tenants = await repository.Tenants.FindAll();
                                outputStore.Set("Tentacle.Tenants", tenants.Where(x => machine.TenantIds.Contains(x.Id)).Select(x => new {x.Id, x.Name}));
                                outputStore.Set("Tentacle.TenantTags", machine.TenantTags);
                            }
                            outputStore.Set("Tentacle.Roles", machine.Roles);
                            var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId);
                            outputStore.Set("Tentacle.MachinePolicy", new {machinePolicy.Id, machinePolicy.Name});
                            outputStore.Set<string>("Tentacle.DisplayName", machine.Name);
                            if (machine.Endpoint is ListeningTentacleEndpointResource)
                                outputStore.Set<string>("Tentacle.Communication.PublicHostName", ((ListeningTentacleEndpointResource) machine.Endpoint).Uri);
                            break;

                        default:
                            if (matchingMachines.Count > 1)
                                throw new ControlledFailureException("This Tentacle is registered multiple times with the server - unable to display configuration");
                            break;
                    }
                }
            }
        }
    }
}
