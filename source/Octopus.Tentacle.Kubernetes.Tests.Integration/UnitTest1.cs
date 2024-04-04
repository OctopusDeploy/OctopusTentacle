using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class UnitTest1: KubernetesIntegrationTest
{
    public UnitTest1(KubernetesClusterFixture kubernetesClusterFixture, KubernetesAgentHelmFixture kubernetesAgentHelmFixture)
        : base(kubernetesClusterFixture, kubernetesAgentHelmFixture)
    {
    }

    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }
}