using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    [ServiceContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1/jobs", Name = "Jobs", SessionMode = SessionMode.NotAllowed)]
    [Suffix("Jobs")]
    public interface IJobService
    {
        [OperationContract]
        JobTicket DeployPackage(DeploymentRequest manifest);

        [OperationContract]
        JobTicket Upgrade();

        [OperationContract]
        JobStatus CheckStatus(JobTicket ticket);

        [OperationContract]
        void CompleteJob(JobTicket ticket);
    }
}