using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobStatus
    {
        public JobStatus()
        {
            Results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        [DataMember]
        public JobQueueState State { get; set; }

        [DataMember]
        public JobTicket Ticket { get; set; }

        [DataMember]
        public string Log { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }

        [DataMember]
        public Dictionary<string, string> Results { get; set; }

        [DataMember]
        public List<JobArtifact> CreatedArtifacts { get; set; }
    }
}