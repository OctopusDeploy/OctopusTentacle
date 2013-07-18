using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class ArtifactChunk
    {
        [DataMember]
        public byte[] Data { get; set; }

        [DataMember]
        public bool IsLastChunk { get; set; }
    }
}