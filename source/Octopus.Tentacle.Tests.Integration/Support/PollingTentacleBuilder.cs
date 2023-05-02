using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        public (IDisposable, Task) DoStuff(int octopusHalibutPort, string octopusThumbprint, string tentaclePollSubscriptionId, CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");

            CreateInstance(configFilePath, instanceName, cancellationToken);
            AddCertificateToTentacle(configFilePath, instanceName, Certificates.TentaclePfxPath, cancellationToken);
            PollOctopusServer(configFilePath, octopusHalibutPort, octopusThumbprint, tentaclePollSubscriptionId);

            return (tempDirectory, RunningTentacle(configFilePath, instanceName, cancellationToken));
        }

        private Task RunningTentacle(string configFilePath, string instanceName, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    RunTentacleCommandInProcess(new [] {"agent", "--config", configFilePath, $"--instance={instanceName}"}, cancellationToken);
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

        private void AddCertificateToTentacle(string configFilePath, string instanceName, string tentaclePfxPath, CancellationToken token)
        {
            RunTentacleCommandInProcess(new [] {"import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}"}, token);
        }

        private void CreateInstance(string configFilePath, string instanceName, CancellationToken token)
        {
            //$tentacle_bin  create-instance --config "$configFilePath" --instance=$name
            RunTentacleCommandInProcess(new [] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"}, token);
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
