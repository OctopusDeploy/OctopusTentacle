using System;
using System.IO;
using System.Linq;
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
        public (IDisposable, Task) Build(int octopusHalibutPort, string octopusThumbprint, string tentaclePollSubscriptionId, CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");

            CreateInstance(configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(configFilePath, instanceName, Certificates.TentaclePfxPath, tempDirectory, cancellationToken);
            PollOctopusServer(configFilePath, octopusHalibutPort, octopusThumbprint, tentaclePollSubscriptionId);

            return (tempDirectory, RunningTentacle(configFilePath, instanceName, tempDirectory, cancellationToken));
        }

        private Task RunningTentacle(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    RunTentacleCommand(new [] {"agent", "--config", configFilePath, $"--instance={instanceName}"}, tmp, cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }, cancellationToken);
        }

        private void PollOctopusServer(string configFilePath, int octopusHalibutPort, string octopusThumbprint, string tentaclePollSubscriptionId)
        {
            // TODO: No, listen: NoListen
            //RunTentacleCommandInProcess("poll-server", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}");

            var startUpConfigFileInstanceRequest = new StartUpConfigFileInstanceRequest(configFilePath);
            using var container = new Octopus.Tentacle.Startup.Tentacle().BuildContainer(startUpConfigFileInstanceRequest);

            var writableTentacleConfiguration = container.Resolve<IWritableTentacleConfiguration>();
            writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(octopusThumbprint)
            {
                Address = new Uri("https://localhost:" + octopusHalibutPort),
                CommunicationStyle = CommunicationStyle.TentacleActive,
                SubscriptionId = tentaclePollSubscriptionId
            });
        }

        private void AddCertificateToTentacle(string configFilePath, string instanceName, string tentaclePfxPath, TemporaryDirectory tmp, CancellationToken token)
        {
            RunTentacleCommand(new [] {"import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}"}, tmp, token);
        }

        private void CreateInstance(string configFilePath, string instanceName, TemporaryDirectory tmp, CancellationToken token)
        {
            //$tentacle_bin  create-instance --config "$configFilePath" --instance=$name
            RunTentacleCommand(new [] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, tmp, token);
        }

        private void RunTentacleCommand(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            RunTentacleCommandOutOfProcess(args, tmp, cancellationToken);
        }
        
        private void RunTentacleCommandOutOfProcess(string[] args, TemporaryDirectory tmp, CancellationToken cancellationToken)
        {
            var assemblyLocation = this.GetType().Assembly.Location;
            var tentacleExe = Path.Combine(Path.GetDirectoryName(assemblyLocation), "Tentacle");
            if (OperatingSystem.IsWindows()) tentacleExe += ".exe";

            var arguments = args.Aggregate((current, next) => current + " " + next);
            var exitCode = SilentProcessRunner.ExecuteCommand(
                tentacleExe,
                arguments,
                tmp.DirectoryPath,
                output => TestContext.WriteLine(output),
                output => TestContext.WriteLine(output),
                output => TestContext.WriteLine(output),
                cancellationToken);
            
            if(cancellationToken.IsCancellationRequested) return;
            if (exitCode != 0)
            {
                throw new Exception("Error running tentacle");
            }
        }

        private void RunTentacleCommandInProcess(string[] args, CancellationToken cancellationToken)
        {
            var res = new Octopus.Tentacle.Startup.Tentacle()
                .RunTentacle(args,
                    () => { },
                    (start) => { },
                    (shutdown) => new DoNothingDisposable(),
                    new TestCommandHostStrategy(cancellationToken),
                    String.Empty,
                    new InMemoryLog());

            if (res == null) throw new Exception("Unknown command");
        }

        private class DoNothingDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
