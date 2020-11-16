using System;

namespace Octopus.Shared.Security
{
    public class CertificateExpectations
    {
        public const string OctopusCertificateName = "Octopus Portal";
        public const string OctopusCertificateFullName = "cn=" + OctopusCertificateName;
        const string OctopusAzureCertificateName = "Octopus Deploy";
        const string OctopusAzureCertificateFullName = "cn=" + OctopusAzureCertificateName;
        public const string TentacleCertificateName = "Octopus Tentacle";
        public const string TentacleCertificateFullName = "cn=" + TentacleCertificateName;

        public static string BuildOctopusAzureCertificateFullName(string azureAccountName)
            => OctopusAzureCertificateFullName + " - " + azureAccountName;
    }
}