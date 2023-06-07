using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleFetcherFactory
    {
        private ITentacleFetcher GetBase()
        {
            return new VersionDependentTentacleFetcher();
        }

        public ITentacleFetcher Create()
        {
            return new TentacleBinaryCache(GetBase());
        }
    }
}