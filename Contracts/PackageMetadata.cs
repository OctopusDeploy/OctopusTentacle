using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class PackageMetadata
    {
        static readonly Regex PackageNameRegex = new Regex("(?<name>.*?)\\.(?<version>[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+)");
        
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
            var fileName = Path.GetFileName(filePath) ?? string.Empty;
            if (fileName.EndsWith("nupkg"))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            }

            var match = PackageNameRegex.Match(fileName);
            if (!match.Success)
            {
                throw new ArgumentException(string.Format("The file name '{0}' is not formatted as a valid NuGet package", filePath));
            }

            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("NuGet package not found", filePath);
            }

            return new PackageMetadata(match.Groups["name"].Value, match.Groups["version"].Value, file.Length);
        }
    }
}