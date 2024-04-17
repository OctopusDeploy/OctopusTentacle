using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodContainerResolver
    {
        Task<string> GetContainerImageForCluster();
    }

    public class KubernetesPodContainerResolver : IKubernetesPodContainerResolver
    {
        readonly IKubernetesClusterService clusterService;

        public KubernetesPodContainerResolver(IKubernetesClusterService clusterService)
        {
            this.clusterService = clusterService;
        }

        static readonly List<Version> KnownLatestContainerTags = new()
        {
            new(1, 26),
            new(1, 27),
            new(1, 28),
            new(1, 29),
        };

        public async Task<string> GetContainerImageForCluster()
        {
            var clusterVersion = await clusterService.GetClusterVersion();

            //find the highest tag for this cluster version
            var tagVersion = KnownLatestContainerTags.FirstOrDefault(tag => tag.Major == clusterVersion.Major && tag.Minor == clusterVersion.Minor);

            var tag = tagVersion?.ToString(2) ?? "latest";

            return $"docker.packages.octopushq.com/octopusdeploy/kubernetes-agent-tools-base:{tag}";
        }
    }
}