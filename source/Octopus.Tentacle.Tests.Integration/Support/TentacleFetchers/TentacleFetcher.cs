using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public static class TentacleFetcher
    {
        public static async Task<string> GetTentacleVersion(string downloadPath, Version version, TentacleRuntime runtime, ILogger logger, CancellationToken cancellationToken)
        {
            if (!TentacleVersions.AllTestedVersionsToDownload.Any(v => v.Equals(version)))
            {
                if (TentacleVersions.VersionsToSkip.Any(v => v.Equals(version)))
                {
                    throw new IgnoreException($"Skipping test for version {version} as it is not supported");
                }
                throw new Exception($"Version {version} must be added to {nameof(TentacleVersions)}.{nameof(TentacleVersions.AllTestedVersionsToDownload)}");
            }
            return await new TentacleFetcherFactory().Create(logger).GetTentacleVersion(downloadPath, version, runtime, cancellationToken);
        }
    }
}
