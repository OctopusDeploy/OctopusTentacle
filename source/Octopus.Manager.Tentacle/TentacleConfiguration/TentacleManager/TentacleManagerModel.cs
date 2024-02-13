using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Manager.Tentacle.Controls;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager
{
    public class TentacleManagerModel : ShellViewModel
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

        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceSelector selector;
        readonly Func<DeleteWizardModel> deleteWizardModelFactory;
        readonly Func<ProxyWizardModel> proxyWizardModelFactory;
        readonly Func<PollingProxyWizardModel> pollingProxyWizardModelFactory;
        readonly Func<SetupTentacleWizardModel> setupTentacleWizardModel;

        public TentacleManagerModel(
            IOctopusFileSystem fileSystem,
            IApplicationInstanceSelector selector,
            ICommandLineRunner commandLineRunner,
            InstanceSelectionModel instanceSelectionModel,
            Func<DeleteWizardModel> deleteWizardModelFactory,
            Func<ProxyWizardModel> proxyWizardModelFactory,
            Func<PollingProxyWizardModel> pollingProxyWizardModelFactory,
            Func<SetupTentacleWizardModel> setupTentacleWizardModel)
            : base(instanceSelectionModel)
        {
            this.fileSystem = fileSystem;
            this.selector = selector;
            this.deleteWizardModelFactory = deleteWizardModelFactory;
            this.proxyWizardModelFactory = proxyWizardModelFactory;
            this.pollingProxyWizardModelFactory = pollingProxyWizardModelFactory;
            this.setupTentacleWizardModel = setupTentacleWizardModel;
            CommandLineRunner = commandLineRunner;
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
        
        public ICommandLineRunner CommandLineRunner { get; }

        public void Load(ApplicationInstanceRecord applicationInstance)
        {
            InstanceName = applicationInstance.InstanceName;
            ConfigurationFilePath = applicationInstance.ConfigurationFilePath;
            Reload(applicationInstance);
        }

        void Reload(ApplicationInstanceRecord applicationInstance)
        {
            var keyStore = LoadConfiguration();

            var home = new HomeConfiguration(ApplicationName.Tentacle, selector);

            HomeDirectory = home.HomeDirectory;

            LogsDirectory = new LoggingConfiguration(home).LogsDirectory;

            serviceWatcher?.Dispose();

            var commandLinePath = CommandLine.PathToTentacleExe();
            ServiceWatcher = new ServiceWatcher(ApplicationName.Tentacle, applicationInstance.InstanceName, commandLinePath);

            var tencon = new Octopus.Tentacle.Configuration.TentacleConfiguration(
                selector,
                home,
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
                var listens = tencon.TrustedOctopusServers.Where(t => t.CommunicationStyle == CommunicationStyle.TentaclePassive ||
                    (t.CommunicationStyle == CommunicationStyle.KubernetesTentacle && t.KubernetesTentacleCommunicationMode == TentacleCommunicationModeResource.Listening)).ToList();
                if (listens.Any())
                {
                    describeTrust.Add($"The Tentacle listens for connections on port {tencon.ServicesPortNumber}.");

                    var thumbprints = listens.Select(l => l.Thumbprint).ReadableJoin();
                    describeTrust.Add(listens.Count == 1
                        ? $"Incoming requests are accepted from the Octopus Server with thumbprint {thumbprints}."
                        : $"Incoming requests are accepted from Octopus Servers with thumbprints {thumbprints}.");
                }

                var polls = tencon.TrustedOctopusServers.Where(s => s.CommunicationStyle == CommunicationStyle.TentacleActive ||
                    (s.CommunicationStyle == CommunicationStyle.KubernetesTentacle && s.KubernetesTentacleCommunicationMode == TentacleCommunicationModeResource.Polling)).ToList();
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

        public DeleteWizardModel StartDeleteWizard()
        {
            return deleteWizardModelFactory();
        }

        public ProxyWizardModelWrapper CreateProxyWizardModelWrapper()
        {
            var proxyWizardModel = CreateProxyWizardModel();
            var wrapper = new ProxyWizardModelWrapper(proxyWizardModel);

            if (PollingProxyConfiguration == null) return wrapper;
            
            var pollingWizardModel = CreatePollingWizardModel();
            wrapper.AddPollingModel(pollingWizardModel);
            return wrapper;
        }

        PollingProxyWizardModel CreatePollingWizardModel()
        {
            var pollingProxyWizardModel = pollingProxyWizardModelFactory();
            ConfigureDefaultValues(pollingProxyWizardModel, PollingProxyConfiguration);
            return pollingProxyWizardModel;
        } 

        ProxyWizardModel CreateProxyWizardModel()
        {
            var proxyWizardModel = proxyWizardModelFactory();
            ConfigureDefaultValues(proxyWizardModel, ProxyConfiguration);
            return proxyWizardModel;
        }

        static void ConfigureDefaultValues(ProxyWizardModel proxyWizardModel, IProxyConfiguration proxyConfiguration)
        {
            proxyWizardModel.ShowProxySettings = true;
            proxyWizardModel.ToggleService = false;
            
            if (!proxyConfiguration.UseDefaultProxy && string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyHost))
            {
                proxyWizardModel.ProxyConfigType = ProxyConfigType.NoProxy;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyHost))
                {
                    proxyWizardModel.ProxyConfigType = ProxyConfigType.CustomProxy;
                    proxyWizardModel.ProxyPassword = string.Empty;
                    proxyWizardModel.ProxyUsername = proxyConfiguration.CustomProxyUsername;
                    proxyWizardModel.ProxyServerHost = proxyConfiguration.CustomProxyHost;
                    proxyWizardModel.ProxyServerPort = proxyConfiguration.CustomProxyPort;
                }
                else if (!string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyUsername))
                {
                    proxyWizardModel.ProxyConfigType = ProxyConfigType.DefaultProxyCustomCredentials;
                    proxyWizardModel.ProxyPassword = string.Empty;
                    proxyWizardModel.ProxyUsername = proxyConfiguration.CustomProxyUsername;
                }
                else
                {
                    proxyWizardModel.ProxyConfigType = ProxyConfigType.DefaultProxy;
                }
            }
        }
        
        public SetupTentacleWizardModel CreateSetupTentacleWizardModel()
        {
            return setupTentacleWizardModel();
        }
    }
}
