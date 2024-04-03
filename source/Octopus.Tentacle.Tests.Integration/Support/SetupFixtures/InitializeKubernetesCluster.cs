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
        readonly string clusterName = $"tentacleint-{DateTime.Now:yyyyMMddhhmmss}";

        CancellationTokenSource cts = new();

        string kindExe = null!;
        TemporaryDirectory tempDir = null!;

        public async Task OneTimeSetUp(ILogger logger)
        {
            tempDir = new TemporaryDirectory();

            var kindDownloader = new KindDownloader(logger);
            kindExe = await kindDownloader.DownloadLatest(tempDir.DirectoryPath, cts.Token);

            var exitCode = SilentProcessRunner.ExecuteCommand(
                kindExe,
                //we give the cluster a unique name
                $"create cluster --name={clusterName}",
                tempDir.DirectoryPath,
                logger.Debug,
                logger.Information,
                logger.Error,
                cts.Token);

            if (exitCode != 0)
            {
                throw new InvalidOperationException("Failed to create KIND Kubernetes Cluster");
            }
        }

        public async Task OneTimeTearDown(ILogger logger)
        {
            await Task.CompletedTask;

            SilentProcessRunner.ExecuteCommand(
                kindExe,
                //delete the cluster for this test run
                $"delete cluster --name={clusterName}",
                tempDir.DirectoryPath,
                s => logger.Debug(s),
                s => logger.Information(s),
                s => logger.Error(s),
                cts.Token);

            cts.Cancel();
            cts.Dispose();

            tempDir.Dispose();
        }
    }
}