using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    [ServiceContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1", Name = "Packages", SessionMode = SessionMode.Required)]
    public interface IPackageService
    {
        [OperationContract(IsInitiating = true)]
        PackageExistance BeginUpload(PackageMetadata metadata);

        [OperationContract]
        void UploadChunk(byte[] data);

        [OperationContract(IsTerminating = true)]
        void FinalizeUpload();
    }
}