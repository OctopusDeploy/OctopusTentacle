using System;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class OctoService
    {
        public OctoService(string pathToOctopusServerExe, string instanceName)
        {
            Executable = pathToOctopusServerExe;
            InstanceName = instanceName;
        }

        public string Executable { get; set; }
        public string InstanceName { get; set; }
    }
}