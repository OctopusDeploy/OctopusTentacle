using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Tests.Integration.Support.Kubernetes;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class InitializeKubernetesCluster : ISetupFixture
    {
        CancellationTokenSource cts = new();
        public async Task OneTimeSetUp(ILogger logger)
        {
            using var tempDir = new TemporaryDirectory();

            var kindDownloader = new KindDownloader(logger);
            var kindExe = await kindDownloader.DownloadLatest(tempDir.DirectoryPath, cts.Token);

            Action<string> log = s => logger.Information(s);
            var exitCode = SilentProcessRunner.ExecuteCommand(
                "chmod",
                $"+x ./kind",
                directoryPath,
                log,
                log,
                log,
                CancellationToken.None);
        }

        public async Task OneTimeTearDown(ILogger logger)
        {
            await Task.CompletedTask;

            cts.Cancel();
            cts.Dispose();

        }
    }
}