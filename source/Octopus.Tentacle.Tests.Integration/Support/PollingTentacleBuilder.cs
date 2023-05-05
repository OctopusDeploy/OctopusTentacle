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

        public (IDisposable, Task) Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");

            CreateInstance(configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(configFilePath, instanceName, Certificates.TentaclePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(
                configFilePath,
                octopusHalibutPort,
                octopusThumbprint ?? Certificates.ServerPublicThumbprint,
                tentaclePollSubscriptionId ?? PollingSubscriptionId.Generate());

            return (tempDirectory, RunningTentacle(configFilePath, instanceName, tempDirectory, cancellationToken));
        }

        private Task RunningTentacle(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    RunTentacleCommand(new[] {"agent", "--config", configFilePath, $"--instance={instanceName}", "--noninteractive"}, tmp, cancellationToken);
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

        private void AddCertificateToTentacle(string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(new[] {"import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void CreateInstance(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommand(new[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
        }

        private void RunTentacleCommand(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommandOutOfProcess(args, tmp, cancellationToken);
        }

        private void RunTentacleCommandOutOfProcess(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                TentacleExeFinder.FindTentacleExe(),
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
}
