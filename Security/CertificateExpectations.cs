using System;

namespace Octopus.Shared.Security
{
    public class CertificateExpectations
    {
        public const string OctopusCertificateName = "Octopus Portal";
        public const string OctopusCertificateFullName = "cn=" + OctopusCertificateName;
        
        public const string OctopusAzureCertificateName = "Octopus Deploy";
        public const string OctopusAzureCertificateFullName = "cn=" + OctopusAzureCertificateName;

        public const string TentacleCertificateName = "Octopus Tentacle";
        public const string TentacleCertificateFullName = "cn=" + TentacleCertificateName;
    }
}