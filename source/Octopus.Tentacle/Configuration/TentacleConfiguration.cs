using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Security;
using Octopus.Shared.Security.Certificates;

namespace Octopus.Tentacle.Configuration
{
    public class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration home;
        readonly ICertificateGenerator certificateGenerator;
        readonly IProxyConfiguration proxyConfiguration;
        readonly IPollingProxyConfiguration pollingProxyConfiguration;
        readonly ILog log;

        public TentacleConfiguration(
            IKeyValueStore settings,
            IHomeConfiguration home, 
            ICertificateGenerator certificateGenerator, 
            IProxyConfiguration proxyConfiguration,
            IPollingProxyConfiguration pollingProxyConfiguration,
            ILog log)
        {
            this.settings = settings;
            this.home = home;
            this.certificateGenerator = certificateGenerator;
            this.proxyConfiguration = proxyConfiguration;
            this.pollingProxyConfiguration = pollingProxyConfiguration;
            this.log = log;
        }

        [Obsolete("This configuration entry is obsolete as of 3.0. It is only used as a Subscription ID where one does not exist.")]
        public string TentacleSquid
        {
            get
            {
                return settings.Get("Octopus.Communications.Squid", (string)null);
            }
        }

        public IEnumerable<OctopusServerConfiguration> TrustedOctopusServers
        {
            get { return settings.Get("Tentacle.Communication.TrustedOctopusServers", new OctopusServerConfiguration[0]); }
            set { settings.Set("Tentacle.Communication.TrustedOctopusServers", (value ?? new OctopusServerConfiguration[0]).ToArray()); }
        }

        public IEnumerable<string> TrustedOctopusThumbprints
        {
            get { return TrustedOctopusServers.Select(s => s.Thumbprint); }
        }

        public IProxyConfiguration ProxyConfiguration
        {
            get { return proxyConfiguration; }
        }

        public IPollingProxyConfiguration PollingProxyConfiguration
        {
            get { return pollingProxyConfiguration; }
        }

        public int ServicesPortNumber
        {
            get { return settings.Get("Tentacle.Services.PortNumber", 10933); }
            set { settings.Set("Tentacle.Services.PortNumber", value); }
        }

        public string ApplicationDirectory
        {
            get
            {
                var path = settings.Get("Tentacle.Deployment.ApplicationDirectory");
                if (string.IsNullOrWhiteSpace(path))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    path = Path.Combine(Path.GetPathRoot(programFiles), "Octopus\\Applications");
                }

                return path;
            }
            set { settings.Set("Tentacle.Deployment.ApplicationDirectory", value); }
        }

        public string PackagesDirectory
        {
            // TODO: Still needed?
            get { return Path.Combine(ApplicationDirectory, "Packages"); }
        }

        public string JournalFilePath
        {
            get { return Path.Combine(home.HomeDirectory, "DeploymentJournal.xml"); }
        }

        public X509Certificate2 TentacleCertificate
        {
            get
            {
                var thumbprint = settings.Get("Tentacle.CertificateThumbprint");
                var encoded = settings.Get("Tentacle.Certificate", protectionScope: DataProtectionScope.LocalMachine);
                return string.IsNullOrWhiteSpace(encoded) ? null : CertificateEncoder.FromBase64String(thumbprint, encoded);
            }
            set
            {
                settings.Set("Tentacle.Certificate", CertificateEncoder.ToBase64String(value), DataProtectionScope.LocalMachine);
                settings.Set("Tentacle.CertificateThumbprint", value.Thumbprint);
            }
        }

        public string ListenIpAddress
        {
            get { return settings.Get("Tentacle.Services.ListenIP", string.Empty); }
            set { settings.Set("Tentacle.Services.ListenIP", value); }
        }

        public bool NoListen
        {
            get { return settings.Get("Tentacle.Services.NoListen", false); }
            set { settings.Set("Tentacle.Services.NoListen", value); }
        }

        public OctopusServerConfiguration LastReceivedHandshake
        {
            get
            {
                var setting = settings.Get("Tentacle.Communication.LastReceivedHandshake");
                if (string.IsNullOrWhiteSpace(setting)) return null;
                return JsonConvert.DeserializeObject<OctopusServerConfiguration>(setting);
            }
            set
            {
                var setting = JsonConvert.SerializeObject(value);
                settings.Set("Tentacle.Communication.LastReceivedHandshake", setting);
            }
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
            TrustedOctopusServers = all;

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
            TrustedOctopusServers = Enumerable.Empty<OctopusServerConfiguration>();
        }

        public void RemoveTrustedOctopusServersWithThumbprint(string toRemove)
        {
            if (toRemove == null) throw new ArgumentNullException("toRemove");
            TrustedOctopusServers = TrustedOctopusServers.Where(t => t.Thumbprint != toRemove);
        }

        public void UpdateTrustedServerThumbprint(string old, string @new)
        {
            var existing = TrustedOctopusServers.SingleOrDefault(m => m.Thumbprint == old);
            if (existing != null)
                existing.Thumbprint = @new;
        }

        public X509Certificate2 GenerateNewCertificate()
        {
            var certificate = certificateGenerator.GenerateNew(CertificateExpectations.TentacleCertificateFullName, log);
            TentacleCertificate = certificate;
            return certificate;
        }

        public void ImportCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException("certificate");
            TentacleCertificate = certificate;
        }
    }
}