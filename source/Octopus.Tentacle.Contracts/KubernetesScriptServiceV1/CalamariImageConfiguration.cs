using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class CalamariImageConfiguration
    {
        [JsonConstructor]
        public CalamariImageConfiguration(string name, string version)
        {
            Name = name;
            Version = version;
        }
        
        public string Name { get; }
        public string Version { get; }
    }
}