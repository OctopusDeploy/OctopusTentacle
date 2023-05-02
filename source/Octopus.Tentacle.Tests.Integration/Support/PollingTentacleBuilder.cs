using System;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        public (IDisposable, Task) DoStuff(int octopusHalibutPort, string octopusThumbprint, string tentaclePollSubscriptionId)
        {
            var tmp = new TemporaryDirectory();

            var instanceName = Guid.NewGuid().ToString().Replace("-", "");

            var configFilePath = Path.Combine(tmp.DirectoryPath, instanceName + ".cfg");
            
            File.WriteAllText("/tmp/current", configFilePath);

            createInstance(configFilePath, instanceName);

            AddCertificateToTentacle(configFilePath, instanceName, Certificates.TentaclePfxPath);

            PollOctopusServer(configFilePath, octopusHalibutPort, octopusThumbprint, tentaclePollSubscriptionId);

            return (tmp, RunTentacle(configFilePath, instanceName));

        }

        private Task RunTentacle(string configFilePath, string instanceName)
        {
            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    RunTentacleCommandInProcess("agent", "--config", configFilePath, $"--instance={instanceName}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });
            return runningTentacle;
        }

        private void PollOctopusServer(string configFilePath, int octopusHalibutPort, string octopusThumbprint, string tentaclePollSubscriptionId)
        {
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

        private void AddCertificateToTentacle(string configFilePath, string instanceName, string tentaclePfxPath)
        {
            RunTentacleCommandInProcess("import-certificate", $"--from-file={tentaclePfxPath}", "--config", configFilePath, $"--instance={instanceName}");
        }

        private void createInstance(string configFilePath, string instanceName)
        {
            
            //$tentacle_bin  create-instance --config "$configFilePath" --instance=$name
            RunTentacleCommandInProcess(new string[] {"create-instance", "--config", configFilePath, $"--instance={instanceName}"});
        }

        private void RunTentacleCommandInProcess(params string[] args)
        {
            var res = new Octopus.Tentacle.Startup.Tentacle()
                .RunTentacle(args, 
                    () => { },
                    (start) => { },
                    (shutdown) => new DoNothingDisposable(),
                    "",
                    new InMemoryLog());

            if (res == null) throw new Exception("Unknown command");
        }
        
        
        
    }
    
    internal class DoNothingDisposable : IDisposable {
        public void Dispose()
        {
        }
    }
}