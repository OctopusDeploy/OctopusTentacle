using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    [ServiceContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1/jobs", Name = "Jobs", SessionMode = SessionMode.Required)]
    [Suffix("Jobs")]
    public interface IJobService
    {
        [OperationContract(IsInitiating = true)]
        JobTicket DeployPackage(DeploymentRequest manifest);

        [OperationContract]
        JobStatus CheckStatus(JobTicket ticket);

        [OperationContract(IsTerminating = true)]
        void CompleteJob(JobTicket ticket);
    }
}