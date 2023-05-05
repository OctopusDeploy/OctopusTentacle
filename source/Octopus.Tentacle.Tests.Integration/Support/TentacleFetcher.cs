using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleFetcher
    {
        public static async Task<string> GetTentacleVersion(string downloadPath, string version)
        {
            return await new TentacleFetcherFactory().Create().GetTentacleVersion(downloadPath, version);
        }
    }
}