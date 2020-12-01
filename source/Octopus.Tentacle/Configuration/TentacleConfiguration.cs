#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Octopus.Client.Model;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Security;
using Octopus.Shared.Security.Certificates;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Configuration
{
    internal class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration home;
        readonly IProxyConfiguration proxyConfiguration;
        readonly IPollingProxyConfiguration pollingProxyConfiguration;

        public TentacleConfiguration(
            IKeyValueStore settings,
            IHomeConfiguration home,
            IProxyConfiguration proxyConfiguration,
            IPollingProxyConfiguration pollingProxyConfiguration)
        {
            this.settings = settings;
            this.home = home;
            this.proxyConfiguration = proxyConfiguration;
            this.pollingProxyConfiguration = pollingProxyConfiguration;
        }

        [Obsolete("This configuration entry is obsolete as of 3.0. It is only used as a Subscription ID where one does not exist.")]
        public string? TentacleSquid => settings.Get("Octopus.Communications.Squid", (string?)null);

        public IEnumerable<OctopusServerConfiguration> TrustedOctopusServers => settings.Get("Tentacle.Communication.TrustedOctopusServers", new OctopusServerConfiguration[0]);

        public IEnumerable<string> TrustedOctopusThumbprints
        {
            get { return TrustedOctopusServers.Select(s => s.Thumbprint); }
        }

        public IProxyConfiguration ProxyConfiguration => proxyConfiguration;

        public IPollingProxyConfiguration PollingProxyConfiguration => pollingProxyConfiguration;

        public int ServicesPortNumber => settings.Get("Tentacle.Services.PortNumber", 10933);

        public string ApplicationDirectory
        {
            get
            {
                var path = settings.Get<string>("Tentacle.Deployment.ApplicationDirectory");
                if (string.IsNullOrWhiteSpace(path))
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

        public string JournalFilePath
        {
            get { return Path.Combine(home.HomeDirectory ?? ".", "DeploymentJournal.xml"); }
        }

        public X509Certificate2? TentacleCertificate
        {
            get
            {
                var thumbprint = settings.Get<string>("Tentacle.CertificateThumbprint");
                var encoded = settings.Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey);
                return string.IsNullOrWhiteSpace(encoded) ? null : CertificateEncoder.FromBase64String(thumbprint, encoded);
            }
        }

        public string? ListenIpAddress => settings.Get("Tentacle.Services.ListenIP", string.Empty);

        public bool NoListen => settings.Get("Tentacle.Services.NoListen", false);

        public OctopusServerConfiguration? LastReceivedHandshake
        {
            get
            {
                var setting = settings.Get<string>("Tentacle.Communication.LastReceivedHandshake");
                if (string.IsNullOrWhiteSpace(setting)) return null;
                return JsonConvert.DeserializeObject<OctopusServerConfiguration>(setting);
            }
        }
    }

    class WritableTentacleConfiguration : TentacleConfiguration, IWritableTentacleConfiguration
    {
        readonly IWritableKeyValueStore settings;
        readonly ICertificateGenerator certificateGenerator;
        readonly ILog log;

        public WritableTentacleConfiguration(IWritableKeyValueStore settings,
            IHomeConfiguration home,
            ICertificateGenerator certificateGenerator,
            IProxyConfiguration proxyConfiguration,
            IPollingProxyConfiguration pollingProxyConfiguration,
            ILog log) : base(settings, home, proxyConfiguration, pollingProxyConfiguration)
        {
            this.settings = settings;
            this.certificateGenerator = certificateGenerator;
            this.log = log;
        }

        public bool SetApplicationDirectory(string directory)
        {
            return settings.Set("Tentacle.Deployment.ApplicationDirectory", directory);
        }

        public bool SetServicesPortNumber(int port)
        {
            return settings.Set("Tentacle.Services.PortNumber", port);
        }

        public bool SetListenIpAddress(string? address)
        {
            return settings.Set("Tentacle.Services.ListenIP", address);
        }

        public bool SetNoListen(bool noListen)
        {
            return settings.Set("Tentacle.Services.NoListen", noListen);
        }

        public bool SetLastReceivedHandshake(OctopusServerConfiguration configuration)
        {
            var setting = JsonConvert.SerializeObject(configuration);
            return settings.Set("Tentacle.Communication.LastReceivedHandshake", setting);
        }

        public bool SetTrustedOctopusServers(IEnumerable<OctopusServerConfiguration>? servers)
        {
            return settings.Set("Tentacle.Communication.TrustedOctopusServers", servers ?? new OctopusServerConfiguration[0]);
        }

        bool SetTentacleCertificate(X509Certificate2 certificate)
        {
            return settings.Set("Tentacle.Certificate", CertificateEncoder.ToBase64String(certificate), ProtectionLevel.MachineKey) &&
                settings.Set("Tentacle.CertificateThumbprint", certificate.Thumbprint);
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
                    log.Info($"Updating {CommTypeToString(configuration.CommunicationStyle)} {configuration.Address} {configuration.Thumbprint} - changing to trust {newThumbprint}");
                else
                    log.Info($"Ignoring {CommTypeToString(configuration.CommunicationStyle)} {configuration.Address} {configuration.Thumbprint} - does not match old thumbprint");

                configuration.Thumbprint = match ? newThumbprint : configuration.Thumbprint;
                return configuration;
            }));
        }

        static string CommTypeToString(CommunicationStyle communicationStyle)
        {
            if (communicationStyle == CommunicationStyle.TentacleActive)
                return "polling tentacle";
            if (communicationStyle == CommunicationStyle.TentaclePassive)
                return "listening tentacle";

            return string.Empty;
        }

        public X509Certificate2 GenerateNewCertificate()
        {
            var certificate = certificateGenerator.GenerateNew(CertificateExpectations.TentacleCertificateFullName, log);
            SetTentacleCertificate(certificate);
            return certificate;
        }

        public void ImportCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException("certificate");
            SetTentacleCertificate(certificate);
        }
    }
}