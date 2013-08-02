using System;
using Octopus.Shared.Packages;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.PackageUpload
{
    public class UploadPackageCommand : IMessageWithLogger
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
    }
}
