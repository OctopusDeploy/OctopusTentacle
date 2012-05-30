using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Security;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfiguration : ITentacleConfiguration
    {
        readonly IWindowsRegistry registry;

        public TentacleConfiguration(IWindowsRegistry registry)
        {
            this.registry = registry;
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
            get { return registry.GetString("Tentacle.Deployment.ApplicationDirectory"); }
            set { registry.Set("Tentacle.Deployment.ApplicationDirectory", value); }
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
    }
}