using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class KubernetesJobScriptExecutionContext : IScriptExecutionContext
    {
        [JsonConstructor]
        public KubernetesJobScriptExecutionContext(string? containerImage)
        {
            ContainerImage = containerImage;
        }

        public string? ContainerImage { get; }
    }
}