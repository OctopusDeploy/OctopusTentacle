namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesUtilities
    {
        public static ulong GetResourceBytes(string sizeString)
        {
            var persistentVolumeSize = new k8s.Models.ResourceQuantity(sizeString);
            return persistentVolumeSize.ToUInt64();
        }
    }
}