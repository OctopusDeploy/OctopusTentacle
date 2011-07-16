using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    [ServiceContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1", Name = "Jobs")]
    public interface IJobService
    {
        [OperationContract]
        JobTicket DeployPackage(DeploymentRequest manifest);

        [OperationContract]
        JobServiceStatus CheckServiceStatus();

        [OperationContract]
        JobStatus CheckStatus(JobTicket ticket);

        [OperationContract]
        void CompleteJob(JobTicket ticket);
    }
}