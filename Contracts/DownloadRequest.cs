using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class DownloadRequest
    {
        [DataMember]
        public PackageMetadata Package { get; set; }

        [DataMember]
        public NuGetFeedProperties Feed { get; set; }

        [DataMember]
        public bool ForcePackageDownload { get; set; }
    }
}