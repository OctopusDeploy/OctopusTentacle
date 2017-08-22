using System;
using System.IO;
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
        static readonly string XmlFormat = "XML";
        static readonly string JsonFormat = "json";
        static readonly string JsonHierarchicalFormat = "json-hierarchical";
        static readonly string[] SupportedFormats = { XmlFormat, JsonFormat, JsonHierarchicalFormat };

        string format = XmlFormat;

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
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}); defaults to {format}", v => format = v);
            apiEndpointOptions = AddOptionSet(new ApiEndpointOptions(Options) { Optional = true });
        }

        protected override void Start()
        {
            base.Start();

            if (!SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
                throw new ControlledFailureException($"The format '{format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");

            DictionaryKeyValueStore outputFile;

            if (string.Equals(format, XmlFormat, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(file))
                {
                    EnsureXmlConfigExists(file);
                    outputFile = new XmlFileKeyValueStore(file, autoSaveOnSet: false, isWriteOnly: true);
                }
                else
                {
                    outputFile = new XmlConsoleKeyValueStore();
                }
            }
            else if (string.Compare(format.Substring(0, 4), JsonFormat, StringComparison.OrdinalIgnoreCase) == 0)
            {
                var useHierarchicalOutput = string.Equals(format, JsonHierarchicalFormat, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(file))
                {
                    if (useHierarchicalOutput)
                        outputFile = new JsonHierarchicalFileKeyValueStore(file, fileSystem, autoSaveOnSet: false, isWriteOnly: true);
                    else
                        outputFile = new JsonFileKeyValueStore(file, fileSystem, autoSaveOnSet: false, isWriteOnly: true);
                }
                else
                {
                    if (useHierarchicalOutput)
                        outputFile = new JsonHierarchicalConsoleKeyValueStore();
                    else
                        outputFile = new JsonConsoleKeyValueStore();
                }
            }
            else
            {
                throw new ControlledFailureException($"The format '{format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");
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
            outputStore.Set("Tentacle.CertificateThumbprint", tentacleConfiguration.Value.TentacleCertificate.Thumbprint);

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
                    if (matchingMachines.Count > 1)
                        throw new ControlledFailureException("This Tentacle is registered multiple times with the server - unable to display configuration");

                    if (matchingMachines.Count == 1)
                    {
                        var machine = matchingMachines.First();
                        var environments = await repository.Environments.FindAll();
                        outputStore.Set("Tentacle.Environments", environments.Where(x => machine.EnvironmentIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                        var tenants = await repository.Tenants.FindAll();
                        outputStore.Set("Tentacle.Tenants", tenants.Where(x => machine.TenantIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                        outputStore.Set("Tentacle.TenantTags", machine.TenantTags);
                        outputStore.Set("Tentacle.Roles", machine.Roles);
                        var machinePolicy = await repository.MachinePolicies.Get(machine.MachinePolicyId);
                        outputStore.Set("Tentacle.MachinePolicy", new { machinePolicy.Id, machinePolicy.Name });
                        outputStore.Set("Tentacle.DisplayName", machine.Name);
                        if (machine.Endpoint is ListeningTentacleEndpointResource)
                            outputStore.Set("Tentacle.Communication.PublicHostName", ((ListeningTentacleEndpointResource)machine.Endpoint).Uri);
                    }
                }
            }

        }

        void EnsureXmlConfigExists(string configurationFile)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile);
            fileSystem.EnsureDirectoryExists(parentDirectory);

            if (!fileSystem.FileExists(configurationFile))
            {
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }
        }
    }
}
