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
        JobTicket DownloadPackage(DownloadRequest request);

        [OperationContract]
        JobTicket RunScript(ScriptRequest request);

        [OperationContract]
        JobTicket Upgrade();

        [OperationContract]
        JobStatus CheckStatus(JobTicket ticket);

        [OperationContract]
        ArtifactChunk GetArtifactChunk(JobTicket ticket, string artifactId, int chunkIndex);

        [OperationContract]
        void CompleteJob(JobTicket ticket);
    }
}