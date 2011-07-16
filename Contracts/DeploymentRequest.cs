using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class DeploymentRequest
    {
        public DeploymentRequest()
        {
        }

        public DeploymentRequest(PackageMetadata package)
        {
            Package = package;
        }

        [DataMember]
        public PackageMetadata Package { get; set; }

        [DataMember]
        public List<Variable> Variables { get; set; }
    }
}