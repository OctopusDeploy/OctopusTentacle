using System;
using System.Threading.Tasks;
using k8s;
using Nito.AsyncEx;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesClusterService
    {
        Task<ClusterVersion> GetClusterVersion();
    }

    public class KubernetesClusterService : KubernetesService, IKubernetesClusterService
    {
        readonly AsyncLazy<ClusterVersion> lazyVersion;
        public KubernetesClusterService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider, log)
        {
            //As the cluster version isn't going to change without restarting, we just cache the version in an AsyncLazy
            lazyVersion = new AsyncLazy<ClusterVersion>(async () =>
            {
                var versionInfo = await Client.Version.GetCodeAsync();
                return ClusterVersion.FromVersionInfo(versionInfo);
            }, AsyncLazyFlags.RetryOnFailure);
        }

        public async Task<ClusterVersion> GetClusterVersion()
            => await lazyVersion;
    }
}