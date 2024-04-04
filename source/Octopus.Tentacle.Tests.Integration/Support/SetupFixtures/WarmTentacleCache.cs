using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Util;
using Serilog;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class WarmTentacleCache : ISetupFixture
    {
        private CancellationTokenSource cts = new();

        public async Task OneTimeSetUp(ILogger logger)
        {
            logger.Fatal("Downloading all tentacles now");

            var tasks = new List<Task>();
            var concurrentDownloads = TeamCityDetection.IsRunningInTeamCity() ? 4 : 1;
            var concurrentDownloadLimiter = new SemaphoreSlim(concurrentDownloads, concurrentDownloads);

            foreach (var tentacleVersion in TentacleVersions.AllTestedVersionsToDownload)
            {
                if (tentacleVersion == TentacleVersions.Current) continue;
                tasks.Add(Task.Run(async () =>
                {
                    using var l = await concurrentDownloadLimiter.LockAsync(cts.Token);
                    await GetTentacleVersion(logger, tentacleVersion);
                }));
            }

            await Task.WhenAll(tasks);
        }

        private async Task GetTentacleVersion(ILogger logger, Version tentacleVersion)
        {
            if (PlatformDetection.IsRunningOnWindows && RuntimeDetection.IsDotNet60)
            {
                await GetTentacleVersionWithRuntime(logger, tentacleVersion, TentacleRuntime.DotNet6);
                await GetTentacleVersionWithRuntime(logger, tentacleVersion, TentacleRuntime.Framework48);
            }
            else
            {
                await GetTentacleVersionWithRuntime(logger, tentacleVersion, DefaultTentacleRuntime.Value);
            }
        }

        private async Task GetTentacleVersionWithRuntime(ILogger logger, Version tentacleVersion, TentacleRuntime tentacleRuntime)
        {
            using var tempDir = new TemporaryDirectory();
            logger.Information($"Will fetch tentacle {tentacleVersion} ({tentacleRuntime.GetDescription()}) if it is not already in cache");
            await TentacleFetcher.GetTentacleVersion(tempDir.DirectoryPath, tentacleVersion, tentacleRuntime, logger, cts.Token);
            logger.Information($"Tentacle {tentacleVersion} ({tentacleRuntime.GetDescription()}) is now in cache");
        }

        public async Task OneTimeTearDown(ILogger logger)
        {
            await Task.CompletedTask;

            cts.Cancel();
            cts.Dispose();
        }
    }
}
