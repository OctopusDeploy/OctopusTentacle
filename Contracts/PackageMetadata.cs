using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class PackageMetadata
    {
        public PackageMetadata()
        {
        }

        public PackageMetadata(string packageId, string version) : this(packageId, version, 0)
        {
        }

        public PackageMetadata(string packageId, string version, long size)
        {
            PackageId = packageId;
            Version = version;
            Size = size;
        }

        [DataMember]
        public string PackageId { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public long Size { get; set; }
    }
}