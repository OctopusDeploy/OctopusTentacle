using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobStatus
    {
        [DataMember]
        public string JobName { get; set; }

        [DataMember]
        public JobState State { get; set; }

        [DataMember]
        public JobTicket Ticket { get; set; }

        [DataMember]
        public string Log { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }
    }
}