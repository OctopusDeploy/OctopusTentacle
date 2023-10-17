using System;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class KubernetesJobScriptExecutionContext : IScriptExecutionContext
    {
        public string ContainerImage { get; }
        public string RegistryUrl { get; }
        public string RegistryUsername { get; }
        public string RegistryPassword { get; }

        [JsonConstructor]
        public KubernetesJobScriptExecutionContext(
            string containerImage,
            string registryUrl,
            string registryUsername,
            string registryPassword)
        {
            ContainerImage = containerImage;
            RegistryUrl = registryUrl;
            RegistryUsername = registryUsername;
            RegistryPassword = registryPassword;
        }
    }
}