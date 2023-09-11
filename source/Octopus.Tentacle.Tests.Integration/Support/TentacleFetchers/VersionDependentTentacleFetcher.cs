using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public class VersionDependentTentacleFetcher : ITentacleFetcher
    {
        private ILogger logger;

        public VersionDependentTentacleFetcher(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<string> GetTentacleVersion(string tmp, Version version, TentacleRuntime runtime, CancellationToken cancellationToken)
        {
            if (PlatformDetection.IsRunningOnNix)
            {
                return await new LinuxTentacleFetcher(logger).GetTentacleVersion(tmp, version, runtime, cancellationToken);
            }

            if (version >= new Version("5.0.0") && version < new Version("6.0.0") && PlatformDetection.IsRunningOnWindows)
            {
                return await new WindowsOnlyNugetBinsTentacleFetcher(logger).GetTentacleVersion(tmp, version, runtime, cancellationToken);
            }
            
            // Nuget cross platform packages only go as far back as 6.0.174
            return await new NugetTentacleFetcher(logger).GetTentacleVersion(tmp, version, runtime, cancellationToken);
        }
    }
}
