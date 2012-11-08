using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Security;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IWindowsRegistry registry;
        readonly IOctopusFileSystem fileSystem;

        public TentacleConfiguration(IWindowsRegistry registry, IOctopusFileSystem fileSystem)
        {
            this.registry = registry;
            this.fileSystem = fileSystem;
        }

        public string[] TrustedOctopusThumbprints
        {
            get
            {
                var value = registry.GetString("Tentacle.Security.TrustedOctopusThumbprints");
                if (string.IsNullOrWhiteSpace(value))
                {
                    return new string[0];
                }

                return value.Split(';', ',').Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToArray();
            }

            set
            {
                registry.Set("Tentacle.Security.TrustedOctopusThumbprints", string.Join(",", value));
            }
        }

        public int ServicesPortNumber
        {
            get { return registry.Get("Tentacle.Services.PortNumber", 10933); }
            set { registry.Set("Tentacle.Services.PortNumber", value); }
        }

        public string ApplicationDirectory
        {
            get
            {
                var path = registry.GetString("Tentacle.Deployment.ApplicationDirectory");
                if (string.IsNullOrWhiteSpace(path))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    path = Path.Combine(Path.GetPathRoot(programFiles), "Octopus\\Applications");
                }

                return path;
            }
            set { registry.Set("Tentacle.Deployment.ApplicationDirectory", value); }
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
                var encoded = registry.GetString("Cert-" + CertificateExpectations.TentacleCertificateFullName);
                return string.IsNullOrWhiteSpace(encoded) ? null : CertificateEncoder.FromBase64String(encoded);
            }

            set
            {
                registry.Set("Cert-" + CertificateExpectations.TentacleCertificateFullName, CertificateEncoder.ToBase64String(value));
            }
        }

        public string ServicesHostName
        {
            get { return registry.Get("Tentacle.Services.HostName", "localhost"); }
            set { registry.Set("Tentacle.Services.HostName", value); }
        }
    }
}