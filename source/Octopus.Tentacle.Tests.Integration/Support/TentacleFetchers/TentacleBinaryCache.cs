using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    public class TentacleBinaryCache : ITentacleFetcher
    {
        private readonly ITentacleFetcher tentacleFetcher;
        private ILogger logger;

        public TentacleBinaryCache(ITentacleFetcher tentacleFetcher, ILogger logger)
        {
            this.tentacleFetcher = tentacleFetcher;
            this.logger = logger;
        }

        private static readonly string cacheDirRunExtension = Guid.NewGuid().ToString("N");

        private static readonly ConcurrentDictionary<(Version, TentacleRuntime), SemaphoreSlim> versionLock = new();

        // TODO: Fun times with Runtimes
        public async Task<string> GetTentacleVersion(string tmp, Version version, TentacleRuntime runtime, CancellationToken cancellationToken)
        {
            using var _ = await versionLock.GetOrAdd((version, runtime), s => new SemaphoreSlim(1, 1)).LockAsync();
            
            var runtimeString = runtime.GetStringValue();

            var tentacleVersionCacheDir = TentacleVersionCacheDir(version.ToString(), runtime);

            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            var sw = Stopwatch.StartNew();

            logger.Information("Will download tentacle: {Version} ({Runtime})", version, runtimeString);
            var tentacleExe = await tentacleFetcher.GetTentacleVersion(tmp, version, runtime, cancellationToken);
            var parentDir = new DirectoryInfo(tentacleExe).Parent;

            AddTentacleIntoCache(parentDir, tentacleVersionCacheDir);

            logger.Information("Downloaded tentacle: {Version} ({Runtime}) in {Time}", version, runtimeString, sw.Elapsed);

            if (Directory.Exists(tentacleVersionCacheDir)) return Path.Combine(tentacleVersionCacheDir, TentacleExeFinder.AddExeExtension("Tentacle"));

            return tentacleExe;
        }

        private static string TentacleVersionCacheDir(string version, TentacleRuntime runtime)
        {
            var cachDirName = "TentacleBinaryCache";
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                cachDirName += cachDirName + cacheDirRunExtension;
            }

            var pathBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            var cacheDir = Path.Combine(pathBase, cachDirName, GetActualRuntimeStringValue(runtime));
            Directory.CreateDirectory(cacheDir);

            var tentacleVersionCacheDir = Path.Combine(cacheDir, version);
            return tentacleVersionCacheDir;
        }

        private static string GetActualRuntimeStringValue(TentacleRuntime runtime)
        {
            // If default, find actual runtime and use that string value
            // If non-default, use what was passed in
            return null;
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
