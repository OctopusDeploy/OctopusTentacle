using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Security;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IKeyValueStore settings;

        public TentacleConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public string[] TrustedOctopusThumbprints
        {
            get
            {
                var value = settings.Get("Tentacle.Security.TrustedOctopusThumbprints");
                if (string.IsNullOrWhiteSpace(value))
                {
                    return new string[0];
                }

                return value.Split(';', ',').Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToArray();
            }

            set
            {
                settings.Set("Tentacle.Security.TrustedOctopusThumbprints", string.Join(",", value));
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

        public void Save()
        {
            settings.Save();
        }
    }
}