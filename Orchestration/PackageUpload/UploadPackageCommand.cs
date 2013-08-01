using System;
using Octopus.Shared.Packages;
using Pipefish;

namespace Octopus.Shared.Orchestration.PackageUpload
{
    public class UploadPackageCommand : IMessage
    {
        public StoredPackage StoredPackage { get; private set; }
        public string DestinationSquid { get; private set; }
        public bool ForceUpload { get; private set; }

        public UploadPackageCommand(StoredPackage storedPackage, string destinationSquid, bool forceUpload)
        {
            StoredPackage = storedPackage;
            DestinationSquid = destinationSquid;
            ForceUpload = forceUpload;
        }
    }
}
