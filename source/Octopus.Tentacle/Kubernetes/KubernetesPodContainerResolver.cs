using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodContainerResolver
    {
        Task<string> GetContainerImageForCluster();
    }

    public class KubernetesPodContainerResolver : IKubernetesPodContainerResolver
    {
        readonly IKubernetesClusterService clusterService;
        readonly IToolsImageVersionMetadataProvider imageVersionMetadataProvider;

        public KubernetesPodContainerResolver(IKubernetesClusterService clusterService, IToolsImageVersionMetadataProvider imageVersionMetadataProvider)
        {
            this.clusterService = clusterService;
            this.imageVersionMetadataProvider = imageVersionMetadataProvider;
        }

        const string DefaultKubernetesAgentToolsImage = "octopusdeploy/kubernetes-agent-tools-base";
        const string FallbackImageTag = "latest";
        
        static readonly List<Version> KnownLatestContainerTags = new()
        {
            new(1, 26),
            new(1, 27),
            new(1, 28),
            new(1, 29),
            new(1, 30),
            new(1, 31),
            new(1, 32),
            new(1, 33),
            new(1, 34),
        };

        public async Task<string> GetContainerImageForCluster()
        {
            var imageRepository = KubernetesConfig.ScriptPodContainerImage; 
            if (imageRepository.IsNullOrEmpty())
            {
                return await GetAgentToolsContainerImage();
            }

            var imageTag = KubernetesConfig.ScriptPodContainerImageTag; 
            return $"{imageRepository}:{imageTag}";
        }

        async Task<string> GetAgentToolsContainerImage()
        {
            var clusterVersion = await clusterService.GetClusterVersion();
            
            var versionMetadata = await imageVersionMetadataProvider.TryGetVersionMetadata();
            if (TryGetImageTagFromVersionMetadata(versionMetadata, clusterVersion, out var imageTag))
            {
                return $"{DefaultKubernetesAgentToolsImage}:{imageTag}";
            }

            return GetFallbackAgentToolsImage(clusterVersion);
        }

        static bool TryGetImageTagFromVersionMetadata(KubernetesAgentToolsImageVersionMetadata? versionMetadata, ClusterVersion clusterVersion, out string imageTag)
        {
            imageTag = "";
            if (versionMetadata is null)
            {
                return false;
            }
            
            var versionDeprecation = versionMetadata.Deprecations.FirstOrDefault(kvp => ClusterVersion.FromVersion(kvp.Key).Equals(clusterVersion));
            if (versionDeprecation.Key is not null)
            {
                imageTag = versionDeprecation.Value.LatestTag;
                return true;
            }

            if (ClusterVersion.FromVersion(versionMetadata.Latest).CompareTo(clusterVersion) < 0)
            {
                imageTag = FallbackImageTag;
                return true;
            }
            
            var imageExists = versionMetadata.ToolVersions.Kubectl.Any(v => ClusterVersion.FromVersion(v).Equals(clusterVersion));
            if (imageExists)
            {
                imageTag = $"{clusterVersion}-{versionMetadata.RevisionHash}";
                return true;
            }
            
            imageTag = FallbackImageTag;
            return true;
        }

        static string GetFallbackAgentToolsImage(ClusterVersion clusterVersion)
        {
            var tagVersion = KnownLatestContainerTags.FirstOrDefault(tag => tag.Major == clusterVersion.Major && tag.Minor == clusterVersion.Minor);

            var tag = tagVersion?.ToString(2) ?? "latest";

            return $"{DefaultKubernetesAgentToolsImage}:{tag}";
        }
    }
}
