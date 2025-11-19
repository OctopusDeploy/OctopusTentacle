using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class KubernetesPodContainerResolverTests
    {
        readonly KubernetesAgentToolsImageVersionMetadata testVersionMetadata = new(new KubernetesAgentToolVersions(new List<Version>
            {
                new("1.31.1"),
                new("1.30.5"),
                new("1.29.9"),
                new("1.28.14")
            }, new List<Version> { new("3.16.1") },
            new List<Version> { new("7.4.5") }), new Version("1.30"), "Juaa5J", new Dictionary<Version, KubernetesAgentToolDeprecation>
        {
            { new Version("1.26"), new KubernetesAgentToolDeprecation("1.26@sha256:a0892db") },
            { new Version("1.27"), new KubernetesAgentToolDeprecation("1.27@sha256:9d1ce87") }
        });

        readonly IToolsImageVersionMetadataProvider mockToolsImageVersionMetadataProvider = Substitute.For<IToolsImageVersionMetadataProvider>();

        [SetUp]
        public void Init()
        {
            mockToolsImageVersionMetadataProvider.TryGetVersionMetadata().Returns(testVersionMetadata);
        }

        [TestCase(30)]
        [TestCase(29)]
        [TestCase(28)]
        public async Task GetContainerImageForCluster_VersionMetadataExists_ClusterVersionSupported_GetsImageWithRevision(int clusterMinorVersion)
        {
            // Arrange
            var clusterService = Substitute.For<IKubernetesClusterService>();
            clusterService.GetClusterVersion().Returns(new ClusterVersion(1, clusterMinorVersion));

            var podContainerResolver = new KubernetesPodContainerResolver(clusterService, mockToolsImageVersionMetadataProvider);

            // Act
            var result = await podContainerResolver.GetContainerImageForCluster();

            // Assert
            result.Should().Be($"octopusdeploy/kubernetes-agent-tools-base:1.{clusterMinorVersion}-Juaa5J");
        }

        [TestCase(27, "1.27@sha256:9d1ce87")]
        [TestCase(26, "1.26@sha256:a0892db")]
        public async Task GetContainerImageForCluster_VersionMetadataExists_ClusterVersionDeprecated_GetsLatestDeprecatedTag(int clusterMinorVersion, string expectedImageTag)
        {
            // Arrange
            var clusterService = Substitute.For<IKubernetesClusterService>();
            clusterService.GetClusterVersion().Returns(new ClusterVersion(1, clusterMinorVersion));

            var podContainerResolver = new KubernetesPodContainerResolver(clusterService, mockToolsImageVersionMetadataProvider);

            // Act
            var result = await podContainerResolver.GetContainerImageForCluster();

            // Assert
            result.Should().Be($"octopusdeploy/kubernetes-agent-tools-base:{expectedImageTag}");
        }

        [Test]
        public async Task GetContainerImageForCluster_VersionMetadataExists_ClusterVersionGreaterThanLatest_FallbackToLatest()
        {
            // Arrange
            var clusterService = Substitute.For<IKubernetesClusterService>();
            clusterService.GetClusterVersion().Returns(new ClusterVersion(1, 31));

            var podContainerResolver = new KubernetesPodContainerResolver(clusterService, mockToolsImageVersionMetadataProvider);

            // Act
            var result = await podContainerResolver.GetContainerImageForCluster();

            // Assert
            result.Should().Be("octopusdeploy/kubernetes-agent-tools-base:latest");
        }

        [Test]
        public async Task GetContainerImageForCluster_VersionMetadataExists_ClusterVersionNotFound_FallbackToLatest()
        {
            // Arrange
            var clusterService = Substitute.For<IKubernetesClusterService>();
            clusterService.GetClusterVersion().Returns(new ClusterVersion(1, 40));

            var podContainerResolver = new KubernetesPodContainerResolver(clusterService, mockToolsImageVersionMetadataProvider);

            // Act
            var result = await podContainerResolver.GetContainerImageForCluster();

            // Assert
            result.Should().Be("octopusdeploy/kubernetes-agent-tools-base:latest");
        }

        [TestCase(35, "latest")]
        [TestCase(34, "1.34")]
        [TestCase(33, "1.33")]
        [TestCase(32, "1.32")]
        [TestCase(31, "1.31")]
        [TestCase(30, "1.30")]
        [TestCase(29, "1.29")]
        [TestCase(28, "1.28")]
        [TestCase(27, "1.27")]
        [TestCase(26, "1.26")]
        [TestCase(25, "latest")]
        public async Task GetContainerImageForCluster_VersionMetadataNotFound_FallBackToKnownTags(int clusterMinorVersion, string expectedImageTag)
        {
            // Arrange
            var clusterService = Substitute.For<IKubernetesClusterService>();
            clusterService.GetClusterVersion().Returns(new ClusterVersion(1, clusterMinorVersion));
            mockToolsImageVersionMetadataProvider.TryGetVersionMetadata().ReturnsNull();

            var podContainerResolver = new KubernetesPodContainerResolver(clusterService, mockToolsImageVersionMetadataProvider);

            // Act
            var result = await podContainerResolver.GetContainerImageForCluster();

            // Assert
            result.Should().Be($"octopusdeploy/kubernetes-agent-tools-base:{expectedImageTag}");
        }
    }
}