using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Configuration;
using Octopus.Manager.Tentacle.Controls;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager
{
    public class TentacleManagerModel : ViewModel
    {
        string configurationFilePath;
        string homeDirectory;
        string logsDirectory;
        ServiceWatcher serviceWatcher;
        string proxyStatus;
        string communicationMode;
        string thumbprint;
        string trust;
        bool pollsServers;

        IOctopusFileSystem fileSystem;
        private readonly IApplicationInstanceSelector selector;

        public TentacleManagerModel(IOctopusFileSystem fileSystem, IApplicationInstanceSelector selector)
        {
            this.fileSystem = fileSystem;
            this.selector = selector;
        }

        public string InstanceName { get; set; }

        public string ConfigurationFilePath
        {
            get => configurationFilePath;
            set
            {
                if (value == configurationFilePath) return;
                configurationFilePath = value;
                OnPropertyChanged();
            }
        }

        public string HomeDirectory
        {
            get => homeDirectory;
            set
            {
                if (value == homeDirectory) return;
                homeDirectory = value;
                OnPropertyChanged();
            }
        }

        public string LogsDirectory
        {
            get => logsDirectory;
            set
            {
                if (value == logsDirectory) return;
                logsDirectory = value;
                OnPropertyChanged();
            }
        }

        public string Thumbprint
        {
            get => thumbprint;
            set
            {
                if (Equals(value, thumbprint)) return;
                thumbprint = value;
                OnPropertyChanged();
            }
        }

        public string Trust
        {
            get => trust;
            set
            {
                if (Equals(value, trust)) return;
                trust = value;
                OnPropertyChanged();
            }
        }

        public ServiceWatcher ServiceWatcher
        {
            get => serviceWatcher;
            set
            {
                if (Equals(value, serviceWatcher)) return;
                serviceWatcher = value;
                OnPropertyChanged();
            }
        }

        public string ProxyStatus
        {
            get => proxyStatus;
            set
            {
                if (value == proxyStatus) return;
                proxyStatus = value;
                OnPropertyChanged();
            }
        }

        public string CommunicationMode
        {
            get => communicationMode;
            set
            {
                if (value == communicationMode) return;
                communicationMode = value;
                OnPropertyChanged();
            }
        }

        public IProxyConfiguration ProxyConfiguration { get; set; }

        public IPollingProxyConfiguration PollingProxyConfiguration { get; set; }

        public void Load(ApplicationInstanceRecord applicationInstance)
        {
            InstanceName = applicationInstance.InstanceName;
            ConfigurationFilePath = applicationInstance.ConfigurationFilePath;
            Reload(applicationInstance);
        }

        void Reload(ApplicationInstanceRecord applicationInstance)
        {
            var keyStore = LoadConfiguration();

            var home = new HomeConfiguration(ApplicationName.Tentacle, keyStore, selector);

            HomeDirectory = home.HomeDirectory;

            LogsDirectory = new LoggingConfiguration(home).LogsDirectory;

            serviceWatcher?.Dispose();

            var commandLinePath = CommandLine.PathToTentacleExe();
            ServiceWatcher = new ServiceWatcher(ApplicationName.Tentacle, applicationInstance.InstanceName, commandLinePath);

            var tencon = new Octopus.Tentacle.Configuration.TentacleConfiguration(
                keyStore,
                new HomeConfiguration(ApplicationName.Tentacle, keyStore, selector),
                new ProxyConfiguration(keyStore),
                new PollingProxyConfiguration(keyStore),
                new SystemLog()
            );

            pollsServers = false;
            Thumbprint = tencon.TentacleCertificate.Thumbprint;
            var describeTrust = new List<string>();
            if (!tencon.TrustedOctopusServers.Any())
            {
                describeTrust.Add("This Tentacle isn't configured to communicate with any Octopus Deploy servers.");
            }
            else
            {
                var listens = tencon.TrustedOctopusServers.Where(t => t.CommunicationStyle == CommunicationStyle.TentaclePassive).ToList();
                if (listens.Any())
                {
                    describeTrust.Add($"The Tentacle listens for connections on port {tencon.ServicesPortNumber}.");

                    var thumbprints = listens.Select(l => l.Thumbprint).ReadableJoin();
                    describeTrust.Add(listens.Count == 1
                        ? $"Incoming requests are accepted from the Octopus Server with thumbprint {thumbprints}."
                        : $"Incoming requests are accepted from Octopus Servers with thumbprints {thumbprints}.");
                }

                var polls = tencon.TrustedOctopusServers.Where(s => s.CommunicationStyle == CommunicationStyle.TentacleActive).ToList();
                if (polls.Any())
                {
                    pollsServers = true;
                    var addresses = polls.Select(p => $"{p.Thumbprint} at {p.Address}").ReadableJoin();
                    describeTrust.Add(polls.Count == 1
                        ? $"The Tentacle polls the Octopus Server with thumbprint {addresses}."
                        : $"The Tentacle polls the Octopus Servers with thumbprints {addresses}.");
                }
            }
            Trust = string.Join(" ", describeTrust);

            ProxyConfiguration = new ProxyConfiguration(keyStore);
            PollingProxyConfiguration = null;
            ProxyStatus = BuildProxyStatus(ProxyConfiguration) + " for web requests";
            CommunicationMode = pollsServers ? "Polling" : "Listening";
            if (pollsServers)
            {
                PollingProxyConfiguration = new PollingProxyConfiguration(keyStore);
                ProxyStatus += BuildProxyStatus(PollingProxyConfiguration, polling: true) + " to poll the Octopus Server";
            }
            ProxyStatus += ".";
        }

        static string BuildProxyStatus(IProxyConfiguration config, bool polling = false)
        {
            var start = polling ? ", and" : "Tentacle";

            return config.ProxyDisabled()
                ? $"{start} is not using a proxy server"
                : config.UsingCustomProxy()
                    ? $"{start} is using a custom proxy server"
                    : $"{start} is using the default proxy server" + (string.IsNullOrWhiteSpace(config.CustomProxyUsername) ? "" : " with custom credentials");
        }

        IWritableKeyValueStore LoadConfiguration()
        {
            return new XmlFileKeyValueStore(fileSystem, ConfigurationFilePath);
        }
    }
}
