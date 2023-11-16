using System;
using System.Threading.Tasks;
using k8s;
using Nito.AsyncEx;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesClusterService
    {
        Task<Version> GetClusterVersion();
    }

    public class KubernetesClusterService : KubernetesService, IKubernetesClusterService
    {
        readonly AsyncLazy<Version> lazyVersion;
        public KubernetesClusterService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
            //As the cluster version isn't going to change without restarting, we just cache the version in an AsyncLazy
            lazyVersion = new AsyncLazy<Version>(async () =>
            {
                var versionInfo = await Client.Version.GetCodeAsync();

                //the git version is in the format "vX.Y.Z" so we trim the "v" from the front
                return Version.Parse(versionInfo.GitVersion.Substring(1));
            });
        }

        public async Task<Version> GetClusterVersion()
            => await lazyVersion;
    }
}