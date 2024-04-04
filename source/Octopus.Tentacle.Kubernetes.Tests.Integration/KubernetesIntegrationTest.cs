using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

[Collection(KubernetesClusterCollection.Name)]
public abstract class KubernetesIntegrationTest : IClassFixture<KubernetesAgentHelmFixture>
{
    readonly KubernetesClusterFixture kubernetesClusterFixture;
    readonly KubernetesAgentHelmFixture kubernetesAgentHelmFixture;

    public KubernetesIntegrationTest(KubernetesClusterFixture kubernetesClusterFixture, KubernetesAgentHelmFixture kubernetesAgentHelmFixture)
    {
        this.kubernetesClusterFixture = kubernetesClusterFixture;
        this.kubernetesAgentHelmFixture = kubernetesAgentHelmFixture;

        this.kubernetesAgentHelmFixture.InstallAgent(this.kubernetesClusterFixture.KubeConfigPath);
    }
}