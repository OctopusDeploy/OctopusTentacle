using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleFetcherFactory
    {
        private ITentacleFetcher GetBase()
        {
            if (PlatformDetection.IsRunningOnNix) return new LinuxTentacleFetcher();
            return new NugetTentacleFetcher();
        }

        public ITentacleFetcher Create()
        {
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                return GetBase();
            }

            return new TentacleBinaryCache(GetBase());
        }
    }
}