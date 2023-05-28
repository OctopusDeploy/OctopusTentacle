using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;
using Serilog;

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

        private static ConcurrentDictionary<string, SemaphoreSlim> versionLock = new();

        public async Task<string> GetTentacleVersion(string tmp, string version)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<TentacleBinaryCache>();
            
            var cachDirName = "TentacleBinaryCache";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                cachDirName += cachDirName + cacheDirRunExtension;
            }

            var pathBase = //Path.GetTempPath();
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            var cacheDir = Path.Combine(pathBase, cachDirName, NugetTentacleFetcher.TentacleBinaryFrameworkForCurrentOs());
            Directory.CreateDirectory(cacheDir);

            var tentacleVersionCacheDir = Path.Combine(cacheDir, version);
            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));
            
            
            logger.Information("Will download tentacle: {Version}", version);
            var semaphore = versionLock.GetOrAdd(version, s => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(CancellationToken.None);
            try
            {
                var tentacleExe = await tentacleFetcher.GetTentacleVersion(tmp, version);
                var parentDir = new DirectoryInfo(tentacleExe).Parent;

                try
                {
                    lock (CacheMutex)
                    {
                        try
                        {
                            parentDir.MoveTo(tentacleVersionCacheDir);
                        }
                        catch (Exception e)
                        {
                            TestContext.Error.WriteLine($"Could not move {parentDir.FullName} to {tentacleVersionCacheDir} attempting to copy files instead. {e}");
                            parentDir.CopyTo(tentacleVersionCacheDir);
                            Directory.Delete(parentDir.FullName, true);
                        }
                    }
                }
                catch (Exception e)
                {
                    TestContext.Error.WriteLine($"Could not copy {parentDir.FullName} to {tentacleVersionCacheDir} tentacle will not be cached. {e}");
                }
                
                logger.Information("Download tentacle: {Version}", version);

                if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

                return tentacleExe;
            }
            finally
            {
                semaphore.Release(1);
            }
        }
    }
}