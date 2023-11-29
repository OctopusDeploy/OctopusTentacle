﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobContainerResolver
    {
        Task<string> GetContainerImageForCluster();
    }

    public class KubernetesJobContainerResolver : IKubernetesJobContainerResolver
    {
        readonly IKubernetesClusterService clusterService;

        public KubernetesJobContainerResolver(IKubernetesClusterService clusterService)
        {
            this.clusterService = clusterService;
        }

        static readonly List<Version> KnownLatestContainerTags = new()
        {
            new(1, 26, 3),
            new(1, 27, 3),
            new(1, 28, 2),
        };

        public async Task<string> GetContainerImageForCluster()
        {
            var clusterVersion = await clusterService.GetClusterVersion();

            //find the highest tag for this cluster version
            var tagVersion = KnownLatestContainerTags.FirstOrDefault(tag => tag.Major == clusterVersion.Major && tag.Minor == clusterVersion.Minor);

            var tag = tagVersion?.ToString(3) ?? "latest";

            return $"octopuslabs/k8s-workertools:{tag}";
        }
    }
}