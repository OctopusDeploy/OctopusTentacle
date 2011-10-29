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

        public bool Equals(JobTicket other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Reference.Equals(Reference);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (JobTicket)) return false;
            return Equals((JobTicket) obj);
        }

        public override int GetHashCode()
        {
            return Reference.GetHashCode();
        }
    }
}