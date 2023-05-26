using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleBinaryCache : ITentacleFetcher
    {
        public static object CacheMutex = new object();

        private readonly ITentacleFetcher tentacleFetcher;

        public TentacleBinaryCache(ITentacleFetcher tentacleFetcher)
        {
            this.tentacleFetcher = tentacleFetcher;
        }

        private static string cacheDirRunExtension = Guid.NewGuid().ToString("N");

        public async Task<string> GetTentacleVersion(string tmp, string version)
        {
            var cachDirName = "TentacleBinaryCache";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                cachDirName += cachDirName + cacheDirRunExtension;
            }
            var cacheDir = Path.Combine(Path.GetTempPath(), cachDirName, NugetTentacleFetcher.TentacleBinaryFrameworkForCurrentOs());
            Directory.CreateDirectory(cacheDir);

            var tentacleVersionCacheDir = Path.Combine(cacheDir, version);
            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            var tentacleExe = await tentacleFetcher.GetTentacleVersion(tmp, version);

            var parentDir = new DirectoryInfo(tentacleExe).Parent;

            try
            {
                lock (CacheMutex)
                {
                    parentDir.MoveTo(tentacleVersionCacheDir);
                }
            }
            catch (Exception e)
            {
                TestContext.Error.WriteLine($"Could not move {parentDir.FullName} to {tentacleVersionCacheDir} tentacle may not be cached locally. {e}");
            }

            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            return tentacleExe;
        }
    }
}