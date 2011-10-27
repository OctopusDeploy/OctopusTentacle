using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobTicket
    {
        public JobTicket(Guid reference)
        {
            Reference = reference;
        }

        [DataMember]
        public Guid Reference { get; set; }
    }
}