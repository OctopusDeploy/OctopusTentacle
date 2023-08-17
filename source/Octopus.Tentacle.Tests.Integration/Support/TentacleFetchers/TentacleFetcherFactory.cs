using System;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public class TentacleFetcherFactory
    {
        private ITentacleFetcher GetBase(ILogger logger)
        {
            return new VersionDependentTentacleFetcher(logger);
        }

        public ITentacleFetcher Create(ILogger logger)
        {
            return new TentacleBinaryCache(GetBase(logger), logger);
        }
    }
}