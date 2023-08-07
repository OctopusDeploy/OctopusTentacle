using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleBinaryCache : ITentacleFetcher
    {
        private readonly ITentacleFetcher tentacleFetcher;

        public TentacleBinaryCache(ITentacleFetcher tentacleFetcher)
        {
            this.tentacleFetcher = tentacleFetcher;
        }

        private static readonly string cacheDirRunExtension = Guid.NewGuid().ToString("N");

        private static readonly ConcurrentDictionary<Version, SemaphoreSlim> versionLock = new();

        public async Task<string> GetTentacleVersion(string tmp, Version version, CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<TentacleBinaryCache>();
            using var _ = await versionLock.GetOrAdd(version, s => new SemaphoreSlim(1, 1)).LockAsync();

            var tentacleVersionCacheDir = TentacleVersionCacheDir(version.ToString());

            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            var sw = Stopwatch.StartNew();

            logger.Information("Will download tentacle: {Version}", version);
            var tentacleExe = await tentacleFetcher.GetTentacleVersion(tmp, version, cancellationToken);
            var parentDir = new DirectoryInfo(tentacleExe).Parent;

            AddTentacleIntoCache(parentDir, tentacleVersionCacheDir);

            logger.Information("Downloaded tentacle: {Version} in {Time}", version, sw.Elapsed);

            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            return tentacleExe;
        }

        private static string TentacleVersionCacheDir(string version)
        {
            var cachDirName = "TentacleBinaryCache";
            if (TentacleExeFinder.IsRunningInTeamCity())
            {
                cachDirName += cachDirName + cacheDirRunExtension;
            }

            var pathBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            var cacheDir = Path.Combine(pathBase, cachDirName, NugetTentacleFetcher.TentacleBinaryFrameworkForCurrentOs());
            Directory.CreateDirectory(cacheDir);

            var tentacleVersionCacheDir = Path.Combine(cacheDir, version);
            return tentacleVersionCacheDir;
        }

        private static void AddTentacleIntoCache(DirectoryInfo? parentDir, string tentacleVersionCacheDir)
        {
            try
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
            catch (Exception e)
            {
                TestContext.Error.WriteLine($"Could not copy {parentDir.FullName} to {tentacleVersionCacheDir} tentacle will not be cached. {e}");
            }
        }
    }
}