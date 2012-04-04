using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class HealthResult
    {
        [DataMember]
        public string MachineName { get; set; }
        
        [DataMember]
        public string RunningAs { get; set; }
    
        [DataMember]
        public string Version { get; set; }
    }
}