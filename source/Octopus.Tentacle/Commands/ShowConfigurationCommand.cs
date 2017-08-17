using System;
using System.IO;
using System.Linq;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security;
using Octopus.Shared.Services;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
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
        readonly ITentacleConfiguration tentacleConfiguration;
        readonly Lazy<IWatchdog> watchdog;
        string file;

        public override bool SuppressConsoleLogging => true;

        public ShowConfigurationCommand(
            IApplicationInstanceSelector instanceSelector,
            IOctopusFileSystem fileSystem,
            ITentacleConfiguration tentacleConfiguration,
            Lazy<IWatchdog> watchdog) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            this.fileSystem = fileSystem;
            this.tentacleConfiguration = tentacleConfiguration;
            this.watchdog = watchdog;

            Options.Add("file=", "Exports the server configuration to a file. If not specified output goes to the console", v => file = v);
            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}); defaults to {format}", v => format = v);
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
                    outputFile = new JsonFileKeyValueStore(file, fileSystem, useHierarchicalOutput, autoSaveOnSet: false, isWriteOnly: true);
                }
                else
                {
                    outputFile = new JsonConsoleKeyValueStore(useHierarchicalOutput);
                }
            }
            else
            {
                throw new ControlledFailureException($"The format '{format}' is not supported. Try {string.Join(" or ", SupportedFormats)}.");
            }

            CollectConfigurationSettings(outputFile);

            outputFile.Save();
        }

        void CollectConfigurationSettings(DictionaryKeyValueStore outputStore)
        {
            var configStore = new XmlFileKeyValueStore(instanceSelector.GetCurrentInstance().ConfigurationPath);

            var oldHomeConfiguration = new HomeConfiguration(ApplicationName.Tentacle, configStore);
            var homeConfiguration = new HomeConfiguration(ApplicationName.Tentacle, outputStore)
            {
                HomeDirectory = oldHomeConfiguration.HomeDirectory
            };

            var certificateGenerator = new CertificateGenerator();
            var newTentacleConfiguration = new TentacleConfiguration(outputStore, homeConfiguration, certificateGenerator, tentacleConfiguration.ProxyConfiguration, tentacleConfiguration.PollingProxyConfiguration, new NullLog())
            {
                ApplicationDirectory = tentacleConfiguration.ApplicationDirectory,
                ListenIpAddress = tentacleConfiguration.ListenIpAddress,
                NoListen = tentacleConfiguration.NoListen,
                ServicesPortNumber = tentacleConfiguration.ServicesPortNumber,
                TrustedOctopusServers = tentacleConfiguration.TrustedOctopusServers
            };

            //we dont want the actual certificate, as its encrypted, and we get a different output everytime
            outputStore.Set("Tentacle.CertificateThumbprint", tentacleConfiguration.TentacleCertificate.Thumbprint);

            var watchdogConfiguration = watchdog.Value.GetConfiguration();
            watchdogConfiguration.WriteTo(outputStore);
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
