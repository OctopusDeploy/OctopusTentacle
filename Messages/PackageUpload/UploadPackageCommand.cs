using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.PackageUpload
{
    public class UploadPackageCommand : IReusableMessage
    {
        public StoredPackage StoredPackage { get; private set; }
        public string DestinationSquid { get; private set; }
        public bool ForceUpload { get; private set; }
        public LoggerReference Logger { get; private set; }

        public UploadPackageCommand(StoredPackage storedPackage, string destinationSquid, bool forceUpload, LoggerReference logger)
        {
            StoredPackage = storedPackage;
            DestinationSquid = destinationSquid;
            ForceUpload = forceUpload;
            Logger = logger;
        }

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new UploadPackageCommand(StoredPackage, DestinationSquid, ForceUpload, newLogger);
        }
    }
}
