using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobSummary
    {
        [DataMember]
        public string JobName { get; set; }

        [DataMember]
        public JobQueueState State { get; set; }

        [DataMember]
        public JobTicket Ticket { get; set; }
    }
}