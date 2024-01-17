#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Diagnostics;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Security;
using Octopus.Tentacle.Security.Certificates;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    internal class TentacleConfiguration : ITentacleConfiguration
    {
        internal const string IsRegisteredSettingName = "Tentacle.Services.IsRegistered";
        internal const string ServicesPortSettingName = "Tentacle.Services.PortNumber";
        internal const string ServicesListenIPSettingName = "Tentacle.Services.ListenIP";
        internal const string ServicesNoListenSettingName = "Tentacle.Services.NoListen";
        internal const string TrustedServersSettingName = "Tentacle.Communication.TrustedOctopusServers";
        internal const string DeploymentApplicationDirectorySettingName = "Tentacle.Deployment.ApplicationDirectory";
        internal const string CertificateSettingName = "Tentacle.Certificate";
        internal const string CertificateThumbprintSettingName = "Tentacle.CertificateThumbprint";
        internal const string LastReceivedHandshakeSettingName = "Tentacle.Communication.LastReceivedHandshake";

        readonly IKeyValueStore settings;
        readonly IHomeConfiguration home;
        readonly IProxyConfiguration proxyConfiguration;
        readonly IPollingProxyConfiguration pollingProxyConfiguration;
        readonly ISystemLog log;

        // these are held in memory for when running without a config file.
        protected static X509Certificate2? CachedCertificate;
        protected static OctopusServerConfiguration? OctopusServerConfiguration;

        public TentacleConfiguration(
            IApplicationInstanceSelector instanceSelector,
            IHomeConfiguration home,
            IProxyConfiguration proxyConfiguration,
            IPollingProxyConfiguration pollingProxyConfiguration,
            ISystemLog log)
        {
            settings = instanceSelector.Current.Configuration ?? throw new Exception("Unable to get KeyValueStore from instanceSelector");
            this.home = home;
            this.proxyConfiguration = proxyConfiguration;
            this.pollingProxyConfiguration = pollingProxyConfiguration;
            this.log = log;
        }

        [Obsolete("This configuration entry is obsolete as of 3.0. It is only used as a Subscription ID where one does not exist.")]
        public string? TentacleSquid => settings.Get("Octopus.Communications.Squid", (string?)null);

        public IEnumerable<OctopusServerConfiguration> TrustedOctopusServers => settings.Get<IEnumerable<OctopusServerConfiguration>>(TrustedServersSettingName) ?? new OctopusServerConfiguration[0];

        public IEnumerable<string> TrustedOctopusThumbprints
        {
            get { return TrustedOctopusServers.Select(s => s.Thumbprint); }
        }

        public IProxyConfiguration ProxyConfiguration => proxyConfiguration;

        public IPollingProxyConfiguration PollingProxyConfiguration => pollingProxyConfiguration;

        public int ServicesPortNumber => settings.Get(ServicesPortSettingName, 10933);

        public bool IsRegistered => settings.Get(IsRegisteredSettingName, false);

        public void WriteTo(IWritableKeyValueStore outputStore, IEnumerable<string> excluding)
        {
            excluding = new HashSet<string>(excluding);
            SetIfNotExcluded(IsRegisteredSettingName, IsRegistered);
            SetIfNotExcluded(ServicesPortSettingName, ServicesPortNumber);
            SetIfNotExcluded(ServicesListenIPSettingName, ListenIpAddress);
            SetIfNotExcluded(ServicesNoListenSettingName, NoListen);
            SetIfNotExcluded(TrustedServersSettingName, TrustedOctopusServers);
            SetIfNotExcluded(DeploymentApplicationDirectorySettingName, ApplicationDirectory);
            SetIfNotExcluded(CertificateSettingName, TentacleCertificate);
            SetIfNotExcluded(CertificateThumbprintSettingName, TentacleCertificate?.Thumbprint ?? string.Empty);
            SetIfNotExcluded(LastReceivedHandshakeSettingName, LastReceivedHandshake);

            void SetIfNotExcluded<T>(string settingName, T value)
            {
                if (!excluding.Contains(settingName))
                {
                    outputStore.Set(settingName, value);
                }
            }
        }

        public string ApplicationDirectory
        {
            get
            {
                var path = settings.Get<string>(DeploymentApplicationDirectorySettingName);
                if (path is null || string.IsNullOrWhiteSpace(path))
                {
                    if (PlatformDetection.IsRunningOnWindows)
                    {
                        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        path = Path.Combine(Path.GetPathRoot(programFiles) ?? ".", "Octopus\\Applications");
                    }
                    else
                    {
                        //this feels wrong... but it's what we're defaulting to with the install scripts
                        //see https://github.com/OctopusDeploy/OctopusTentacle/blob/d3a0fca5bb67c49b5f594077ff2b3da8ba377ad3/scripts/configure-tentacle.sh#L47
                        path = "/home/Octopus/Applications";
                    }
                }

                return path;
            }
        }

        public string PackagesDirectory
        {
            // TODO: Still needed?
            get { return Path.Combine(ApplicationDirectory, "Packages"); }
        }

        public string JournalFilePath => Path.Combine(home.HomeDirectory ?? ".", "DeploymentJournal.xml");
        public string PackageRetentionJournalPath => Path.Combine(home.HomeDirectory ?? ".", "PackageRetentionJournal.json");

        public X509Certificate2? TentacleCertificate
        {
            get
            {
                if (CachedCertificate != null)
                    return CachedCertificate;
                var thumbprint = settings.Get(CertificateThumbprintSettingName);
                if (string.IsNullOrWhiteSpace(thumbprint))
                {
                    return null;
                }

                var encoded = settings.Get(CertificateSettingName, protectionLevel: ProtectionLevel.MachineKey);
                return encoded is null || string.IsNullOrWhiteSpace(encoded) ? null : CertificateEncoder.FromBase64String(thumbprint!, encoded, log);
            }
        }

        public string? ListenIpAddress => settings.Get(ServicesListenIPSettingName, string.Empty);

        public bool NoListen => settings.Get(ServicesNoListenSettingName, false);

        public OctopusServerConfiguration? LastReceivedHandshake
        {
            get
            {
                if (OctopusServerConfiguration != null)
                    return OctopusServerConfiguration;

                var setting = settings.Get<string>(LastReceivedHandshakeSettingName);
                if (setting is null || string.IsNullOrWhiteSpace(setting)) return null;
                return JsonConvert.DeserializeObject<OctopusServerConfiguration>(setting);
            }
        }
    }

    class WritableTentacleConfiguration : TentacleConfiguration, IWritableTentacleConfiguration
    {
        readonly IWritableKeyValueStore settings;
        readonly ICertificateGenerator certificateGenerator;
        readonly ISystemLog log;

        public WritableTentacleConfiguration(
            IApplicationInstanceSelector instanceSelector,
            IHomeConfiguration home,
            ICertificateGenerator certificateGenerator,
            IProxyConfiguration proxyConfiguration,
            IPollingProxyConfiguration pollingProxyConfiguration,
            ISystemLog log) : base(instanceSelector, home, proxyConfiguration, pollingProxyConfiguration, log)
        {
            settings = instanceSelector.Current.WritableConfiguration ?? throw new Exception("Unable to load WritableKeyValueStore from instanceSelector");
            this.certificateGenerator = certificateGenerator;
            this.log = log;
        }

        public bool SetApplicationDirectory(string directory)
        {
            return settings.Set(DeploymentApplicationDirectorySettingName, directory);
        }

        public bool SetServicesPortNumber(int port)
        {
            return settings.Set(ServicesPortSettingName, port);
        }

        public bool SetIsRegistered(bool isRegistered = true)
        {
            return settings.Set(IsRegisteredSettingName, isRegistered);
        }

        public bool SetListenIpAddress(string? address)
        {
            return settings.Set(ServicesListenIPSettingName, address);
        }

        public bool SetNoListen(bool noListen)
        {
            return settings.Set(ServicesNoListenSettingName, noListen);
        }

        public bool SetLastReceivedHandshake(OctopusServerConfiguration configuration)
        {
            OctopusServerConfiguration = configuration;
            var setting = JsonConvert.SerializeObject(configuration);
            return settings.Set(LastReceivedHandshakeSettingName, setting);
        }

        public bool SetTrustedOctopusServers(IEnumerable<OctopusServerConfiguration>? servers)
        {
            return settings.Set(TrustedServersSettingName, servers ?? new OctopusServerConfiguration[0]);
        }

        bool SetTentacleCertificate(X509Certificate2 certificate)
        {
            return settings.Set(CertificateSettingName, CertificateEncoder.ToBase64String(certificate), ProtectionLevel.MachineKey) &&
                settings.Set(CertificateThumbprintSettingName, certificate.Thumbprint);
        }

        public bool AddOrUpdateTrustedOctopusServer(OctopusServerConfiguration machine)
        {
            if (machine == null) throw new ArgumentNullException("machine");
            var result = false;

            if (!string.IsNullOrWhiteSpace(machine.Squid))
                machine.Squid = NormalizeSquid(machine.Squid);

            var all = TrustedOctopusServers.ToList();

            var existing = all.SingleOrDefault(m => AreEqual(m, machine));

            if (existing != null)
            {
                result = true;
                all.Remove(existing);
            }

            all.Add(machine);
            SetTrustedOctopusServers(all);

            return result;
        }

        static bool AreEqual(OctopusServerConfiguration left, OctopusServerConfiguration right)
        {
            var thumbprintsMatch = string.Compare(left.Thumbprint, right.Thumbprint, StringComparison.OrdinalIgnoreCase) == 0;
            var addressesMatch = Uri.Compare(left.Address, right.Address, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;

            return thumbprintsMatch && addressesMatch;
        }

        static string NormalizeSquid(string squid)
        {
            return squid.ToUpperInvariant();
        }

        public void ResetTrustedOctopusServers()
        {
            SetTrustedOctopusServers(Enumerable.Empty<OctopusServerConfiguration>());
        }

        public void RemoveTrustedOctopusServersWithThumbprint(string toRemove)
        {
            if (toRemove == null) throw new ArgumentNullException("toRemove");
            SetTrustedOctopusServers(TrustedOctopusServers.Where(t => t.Thumbprint != toRemove));
        }

        public void UpdateTrustedServerThumbprint(string oldThumbprint, string newThumbprint)
        {
            log.Info($"Finding existing Octopus Server registrations trusting the thumbprint {oldThumbprint} and updating them to trust the thumbprint {newThumbprint}:");
            SetTrustedOctopusServers(TrustedOctopusServers.Select(configuration =>
            {
                var match = string.Equals(configuration.Thumbprint, oldThumbprint, StringComparison.OrdinalIgnoreCase);

                if (match)
                    log.Info($"Updating {CommTypeToString(configuration.CommunicationStyle, configuration.KubernetesTentacleCommunicationMode)} {configuration.Address} {configuration.Thumbprint} - changing to trust {newThumbprint}");
                else
                    log.Info($"Ignoring {CommTypeToString(configuration.CommunicationStyle, configuration.KubernetesTentacleCommunicationMode)} {configuration.Address} {configuration.Thumbprint} - does not match old thumbprint");

                configuration.Thumbprint = match ? newThumbprint : configuration.Thumbprint;
                return configuration;
            }));
        }

        static string CommTypeToString(CommunicationStyle communicationStyle, TentacleCommunicationModeResource? kubernetesTentacleCommunicationBehaviour = null)
        {
            kubernetesTentacleCommunicationBehaviour ??= TentacleCommunicationModeResource.Polling;

            return communicationStyle switch
            {
                CommunicationStyle.TentacleActive => "polling tentacle",
                CommunicationStyle.TentaclePassive => "listening tentacle",
                CommunicationStyle.KubernetesTentacle =>
                    "kubernetes tentacle" + (kubernetesTentacleCommunicationBehaviour == TentacleCommunicationModeResource.Polling ? " (polling)" : " (listening)"),
                _ => string.Empty
            };
        }

        public X509Certificate2 GenerateNewCertificate()
        {
            var certificate = certificateGenerator.GenerateNew(CertificateExpectations.TentacleCertificateFullName);

            // we write to the config, if there is one, else just hold in memory for the transient tentacles/workers
            try
            {
                SetTentacleCertificate(certificate);
            }
            catch (InvalidOperationException)
            {
                log.Warn("Unable to save Certificate. Storing it in local memory");
                CachedCertificate = certificate;
            }

            return certificate;
        }

        public void ImportCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            SetTentacleCertificate(certificate);
        }
    }
}