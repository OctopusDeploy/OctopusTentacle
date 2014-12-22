using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.PackageUpload
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
