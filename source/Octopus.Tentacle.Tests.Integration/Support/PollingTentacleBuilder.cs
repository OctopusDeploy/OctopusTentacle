using System;
using System.IO;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder
    {
        public void DoStuff()
        {
            using var tmp = new TemporaryDirectory();

            var instanceName = Guid.NewGuid().ToString().Replace("-", "");

            var configFilePath = Path.Combine(tmp.DirectoryPath, instanceName + ".cfg");

            createInstance(configFilePath, instanceName);

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