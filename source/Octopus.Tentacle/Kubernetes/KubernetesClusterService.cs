using System;
using System.Threading.Tasks;
using k8s;
using Nito.AsyncEx;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesClusterService
    {
        Task<ClusterVersion> GetClusterVersion();
    }

    public class KubernetesClusterService : KubernetesService, IKubernetesClusterService
    {
        readonly AsyncLazy<ClusterVersion> lazyVersion;
        public KubernetesClusterService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
            //As the cluster version isn't going to change without restarting, we just cache the version in an AsyncLazy
            lazyVersion = new AsyncLazy<ClusterVersion>(async () =>
            {
                var versionInfo = await Client.Version.GetCodeAsync();
                return KubernetesVersionParser.ParseClusterVersion(versionInfo);
            });
        }

        public async Task<ClusterVersion> GetClusterVersion()
            => await lazyVersion;
    }
}