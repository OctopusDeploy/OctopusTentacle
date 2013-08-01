using System;

namespace Octopus.Shared.Orchestration.PackageUpload
{
    public class PackageUploadResult : ResultMessage
    {
        public PackageUploadResult(bool wasSuccessful, string details)
            : base(wasSuccessful, details)
        {
        }
    }
}
