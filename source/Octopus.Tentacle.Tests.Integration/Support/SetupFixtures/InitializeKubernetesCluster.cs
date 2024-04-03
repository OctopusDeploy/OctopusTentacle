using System;
using System.Diagnostics;
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

            var sw = new Stopwatch();
            sw.Restart();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                kindExe,
                //we give the cluster a unique name
                $"create cluster --name={clusterName} --kubeconfig=\"{clusterName}.config\"",
                tempDir.DirectoryPath,
                logger.Debug,
                logger.Information,
                logger.Error,
                cts.Token);

            sw.Stop();

            if (exitCode != 0)
            {
                throw new InvalidOperationException("Failed to create Kind Kubernetes cluster");
            }

            logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);
        }

        public async Task OneTimeTearDown(ILogger logger)
        {
            await Task.CompletedTask;

            var exitCode = SilentProcessRunner.ExecuteCommand(
                kindExe,
                //delete the cluster for this test run
                $"delete cluster --name={clusterName}",
                tempDir.DirectoryPath,
                logger.Debug,
                logger.Information,
                logger.Error,
                cts.Token);

            if (exitCode != 0)
            {
                logger.Error("Failed to delete Kind kubernetes cluster {ClusterName}", clusterName);
            }

            cts.Cancel();
            cts.Dispose();

            tempDir.Dispose();
        }
    }
}