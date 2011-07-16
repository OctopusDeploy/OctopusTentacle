using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class JobServiceStatus
    {
        public JobServiceStatus()
        {
            ActiveJobs = new List<JobSummary>();
        }

        [DataMember]
        public string MachineName { get; set; }

        [DataMember]
        public string VersionNumber { get; set; }

        [DataMember]
        public List<JobSummary> ActiveJobs { get; set; }
    }
}