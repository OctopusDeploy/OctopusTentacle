using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        readonly int octopusHalibutPort;
        string octopusThumbprint;
        Uri? tentaclePollSubscriptionId;
        private string? tentacleExePath;

        public PollingTentacleBuilder(int octopusHalibutPort, string octopusThumbprint)
        {
            this.octopusHalibutPort = octopusHalibutPort;
            this.octopusThumbprint = octopusThumbprint;
        }

        public PollingTentacleBuilder WithTentaclePollSubscription(Uri tentaclePollSubscriptionId)
        {
            this.tentaclePollSubscriptionId = tentaclePollSubscriptionId;
            return this;
        }

        public PollingTentacleBuilder WithTentacleExe(string tentacleExe)
        {
            tentacleExePath = tentacleExe;
            return this;
        }

        internal RunningTestTentacle Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = this.tentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var subscriptionId = tentaclePollSubscriptionId ?? PollingSubscriptionId.Generate();
            CreateInstance(tentacleExe, configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(tentacleExe, configFilePath, instanceName, Certificates.TentaclePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(
                configFilePath,
                octopusHalibutPort,
                octopusThumbprint ?? Certificates.ServerPublicThumbprint,
                subscriptionId);

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            return new RunningTestTentacle(subscriptionId, tempDirectory, cts, RunningTentacle(tentacleExe, configFilePath, instanceName, tempDirectory, cts.Token));
        }

        private Task RunningTentacle(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    RunTentacleCommand(tentacleExe, new[] {"agent", "--config", configFilePath, $"--instance={instanceName}", "--noninteractive"}, tmp, cancellationToken);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                    throw;
                }
            }, cancellationToken);
        }

        private void ConfigureTentacleToPollOctopusServer(string configFilePath, int octopusHalibutPort, string octopusThumbprint, Uri tentaclePollSubscriptionId)
        {
            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Program(new string[] { }).BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(octopusThumbprint)
            {
                Address = new Uri("https://localhost:" + octopusHalibutPort),
                CommunicationStyle = CommunicationStyle.TentacleActive,
                SubscriptionId = tentaclePollSubscriptionId.ToString()
            });

            writableTentacleConfiguration.SetNoListen(true);
        }

        private void AddCertificateToTentacle(string tentacleExe, string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] {"import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void CreateInstance(string tentacleExe, string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(tentacleExe, new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void RunTentacleCommand(string tentacleExe, string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommandOutOfProcess(tentacleExe, args, tmp, cancellationToken);
        }

        private void RunTentacleCommandOutOfProcess(string tentacleExe, string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                tentacleExe,
                String.Join(" ", args),
                tmp.DirectoryPath,
                TestContext.WriteLine,
                TestContext.WriteLine,
                TestContext.WriteLine,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (exitCode != 0)
            {
                throw new Exception("Tentacle returns non zero exit code: " + exitCode);
            }
        }
    }

    class RunningTestTentacle : IDisposable
    {
        public Uri ServiceUri { get; }
        private TemporaryDirectory TemporaryDirectory;
        private CancellationTokenSource cts;
        public Task Task { get; }

        public RunningTestTentacle(Uri serviceUri, TemporaryDirectory temporaryDirectory, CancellationTokenSource cts, Task task)
        {
            TemporaryDirectory = temporaryDirectory;
            this.cts = cts;
            Task = task;
            ServiceUri = serviceUri;
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
            TemporaryDirectory.Dispose();
        }
    }
}
