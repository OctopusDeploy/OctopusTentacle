using System;

namespace Octopus.Shared.Orchestration.PackageUpload
{
    public class TentaclePackageStoredResult : ResultMessage
    {
        public TentaclePackageStoredResult(bool wasSuccessful, string details)
            : base(wasSuccessful, details)
        {
        }
    }
}
