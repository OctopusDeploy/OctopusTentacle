namespace Octopus.Tentacle.Configuration
{
    public class StartUpKubernetesConfigMapInstanceRequest : StartUpRegistryInstanceRequest
    {
        public StartUpKubernetesConfigMapInstanceRequest(string instanceName) : base(instanceName)
        {
        }
    }
}