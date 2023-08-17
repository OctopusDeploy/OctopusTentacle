using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleFetcher
    {
        public static async Task<string> GetTentacleVersion(string downloadPath, Version version, CancellationToken cancellationToken)
        {
            if (!TentacleVersions.AllTestedVersionsToDownload.Any(v => v.Equals(version)))
            {
                throw new Exception($"Version {version} must be added to {nameof(TentacleVersions)}.{nameof(TentacleVersions.AllTestedVersionsToDownload)}");
            }
            return await new TentacleFetcherFactory().Create().GetTentacleVersion(downloadPath, version, cancellationToken);
        }
    }
}