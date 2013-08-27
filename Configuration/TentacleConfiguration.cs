using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Security;
using Octopus.Shared.Communications;
using Octopus.Shared.Security;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IKeyValueStore settings;
        readonly ICommunicationsConfiguration communicationsConfiguration;

        public TentacleConfiguration(IKeyValueStore settings, ICommunicationsConfiguration communicationsConfiguration)
        {
            this.settings = settings;
            this.communicationsConfiguration = communicationsConfiguration;
        }

        public IEnumerable<OctopusServerConfiguration> TrustedOctopusServers
        {
            get
            {
                var setting = settings.Get("Tentacle.Communication.TrustedOctopusServers");
                if (string.IsNullOrWhiteSpace(setting)) return Enumerable.Empty<OctopusServerConfiguration>();
                return JsonConvert.DeserializeObject<OctopusServerConfiguration[]>(setting);
            }
            private set
            {
                var setting = JsonConvert.SerializeObject(value.ToArray());
                settings.Set("Tentacle.Communication.TrustedOctopusServers", setting);
            }
        }

        public void AddTrustedOctopusServer(OctopusServerConfiguration machine)
        {
            if (machine == null) throw new ArgumentNullException("machine");

            if (!string.IsNullOrWhiteSpace(machine.Squid))
                machine.Squid = NormalizeSquid(machine.Squid);

            var all = TrustedOctopusServers.ToList();
            
            var existing = all.SingleOrDefault(m => m.Address == machine.Address || m.Squid != null && machine.Squid != null && m.Squid == machine.Squid);

            if (existing != null)
                all.Remove(existing);

            foreach (var duplicate in all.Where(m => m.Address == machine.Address && m.Squid == machine.Squid && m.Thumbprint == machine.Thumbprint))
            {
                all.Remove(duplicate);
            }            

            all.Add(machine);
            TrustedOctopusServers = all;
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

        public IEnumerable<string> TrustedOctopusThumbprints
        {
            get
            {
                return TrustedOctopusServers.Select(s => s.Thumbprint);
            }
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
            get { return Path.Combine(ApplicationDirectory, ".Tentacle\\Packages"); }
        }

        public string LogsDirectory
        {
            get { return Path.Combine(ApplicationDirectory, ".Tentacle\\Logs"); }
        }

        public string JournalFilePath
        {
            get { return Path.Combine(ApplicationDirectory, ".Tentacle\\Deployments.xml"); }
        }

        public X509Certificate2 TentacleCertificate
        {
            get
            {
                var encoded = settings.Get("Cert-" + CertificateExpectations.TentacleCertificateFullName);
                return string.IsNullOrWhiteSpace(encoded) ? null : CertificateEncoder.FromBase64String(encoded);
            }

            set
            {
                settings.Set("Cert-" + CertificateExpectations.TentacleCertificateFullName, CertificateEncoder.ToBase64String(value));
            }
        }

        public string ServicesHostName
        {
            get { return settings.Get("Tentacle.Services.HostName", "localhost"); }
            set { settings.Set("Tentacle.Services.HostName", value); }
        }

        public string Squid
        {
            get { return communicationsConfiguration.Squid; }
            set { communicationsConfiguration.Squid = value; }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}