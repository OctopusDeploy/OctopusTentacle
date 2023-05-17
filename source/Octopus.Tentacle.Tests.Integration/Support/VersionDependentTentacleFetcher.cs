using System.Threading.Tasks;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class VersionDependentTentacleFetcher : ITentacleFetcher
    {
        public async Task<string> GetTentacleVersion(string tmp, string version)
        {
            if (PlatformDetection.IsRunningOnNix)
            {
                return await new LinuxTentacleFetcher().GetTentacleVersion(tmp, version);
            }

            if (version.StartsWith("5.") && PlatformDetection.IsRunningOnWindows)
            {
                return await new WindowsOnlyNugetBinsTentacleFetcher().GetTentacleVersion(tmp, version);
            }
            
            // Nuget cross platform packages only go as far back as 6.0.174
            return await new NugetTentacleFetcher().GetTentacleVersion(tmp, version);
        }
    }
}