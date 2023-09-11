using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public interface ITentacleFetcher
    {
        /// <summary>
        /// Downloads the specified version of the Tentacle binaries from some place.
        /// 
        /// </summary>
        /// <param name="tmp">A directory that may be used for holding the binaries or used while downloading the binaries.</param>
        /// <param name="version">The version to download.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The full path to the tentacle executable.</returns>
        public Task<string> GetTentacleVersion(string tmp, Version version, TentacleRuntime runtime, CancellationToken cancellationToken);
    }
}
