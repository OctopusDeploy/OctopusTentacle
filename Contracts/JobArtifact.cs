using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobArtifact
    {
        [DataMember]
        public string ArtifactId { get; set; }

        [DataMember]
        public string OriginalFilename { get; set; }
    }
}
