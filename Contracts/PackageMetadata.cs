using System;
using System.IO;
using System.Runtime.Serialization;
using NuGet;

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

        public static PackageMetadata FromFile(string filePath)
        {
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException(string.Format("Could not find NuGet package file '{0}'", filePath), filePath);
            }

            var package = new ZipPackage(filePath);
            var id = package.Id;
            var version = package.Version.ToString();

            return new PackageMetadata(id, version, file.Length);
        }
    }
}