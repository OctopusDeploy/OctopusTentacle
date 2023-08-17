using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class WarmTentacleCache : ISetupFixture
        {
            private CancellationTokenSource cts = new CancellationTokenSource();
            
            public void OneTimeSetUp(ILogger logger)
            {
                logger.Fatal("Downloading all tentacles now");
                
                var tasks = new List<Task>();
                var concurrentDownloads = TentacleExeFinder.IsRunningInTeamCity() ? 4 : 1;
                var concurrentDownloadLimiter = new SemaphoreSlim(concurrentDownloads, concurrentDownloads);
                foreach (var tentacleVersion in TentacleTypesAndCommonVersionsToTest.StandardVersion)
                {
                    if(tentacleVersion == TentacleVersions.Current) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        using var l = await concurrentDownloadLimiter.LockAsync(cts.Token);
                        using var temporaryDirectory = new TemporaryDirectory();
                        logger.Information($"Will fetch tentacle {tentacleVersion} if it is not already in cache");
                        await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, cts.Token);
                        logger.Information($"Tentacle {tentacleVersion} is now in cache");
                    }));
                }
                
                Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
            
            public void OneTimeTearDown(ILogger logger)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    
}