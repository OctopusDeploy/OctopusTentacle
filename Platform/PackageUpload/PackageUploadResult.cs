using System;

namespace Octopus.Shared.Platform.PackageUpload
{
    public class PackageUploadResult : ResultMessage
    {
        public PackageUploadResult(bool wasSuccessful, string details)
            : base(wasSuccessful, details)
        {
        }
    }
}
