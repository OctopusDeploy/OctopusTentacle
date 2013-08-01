using System;

namespace Octopus.Shared.Platform.PackageUpload
{
    public class TentaclePackageStoredResult : ResultMessage
    {
        public TentaclePackageStoredResult(bool wasSuccessful, string details)
            : base(wasSuccessful, details)
        {
        }
    }
}
