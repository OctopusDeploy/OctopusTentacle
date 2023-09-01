﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class WarmTentacleCache : ISetupFixture
        {
            private CancellationTokenSource cts = new();
            
            public void OneTimeSetUp(ILogger logger)
            {
                logger.Fatal("Downloading all tentacles now");
                
                var tasks = new List<Task>();
                var concurrentDownloads = TeamCityDetection.IsRunningInTeamCity() ? 4 : 1;
                var concurrentDownloadLimiter = new SemaphoreSlim(concurrentDownloads, concurrentDownloads);
                
                foreach (var tentacleVersion in TentacleVersions.AllTestedVersionsToDownload)
                {
                    if(tentacleVersion == TentacleVersions.Current) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        using var l = await concurrentDownloadLimiter.LockAsync(cts.Token);
                        using var temporaryDirectory = new TemporaryDirectory();
                        await GetTentacle(logger, temporaryDirectory, tentacleVersion);
                    }));
                }
                
                Task.WhenAll(tasks).GetAwaiter().GetResult();
            }

            private async Task GetTentacle(ILogger logger, TemporaryDirectory temporaryDirectory, Version tentacleVersion)
            {
                if (PlatformDetection.IsRunningOnWindows && RuntimeDetection.IsDotNet60)
                {
                    logger.Information($"Will fetch tentacle {tentacleVersion} ({TentacleRuntime.DotNet6.GetStringValue()}) if it is not already in cache");
                    await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, TentacleRuntime.DotNet6, logger, cts.Token);
                    logger.Information($"Tentacle {tentacleVersion} ({TentacleRuntime.DotNet6.GetStringValue()}) is now in cache");
                    
                    logger.Information($"Will fetch tentacle {tentacleVersion} ({TentacleRuntime.Framework48.GetStringValue()}) if it is not already in cache");
                    await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, TentacleRuntime.Framework48, logger, cts.Token);
                    logger.Information($"Tentacle {tentacleVersion} ({TentacleRuntime.Framework48.GetStringValue()}) is now in cache");
                }
                else
                {
                    logger.Information($"Will fetch tentacle {tentacleVersion} if it is not already in cache");
                    await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, TentacleRuntime.Default, logger, cts.Token);
                    logger.Information($"Tentacle {tentacleVersion} is now in cache");
                }
            }

            public void OneTimeTearDown(ILogger logger)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    
}
