using Octopus.Tentacle.CommonTestUtils;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public static class TestKubernetesCluster
{
    public static string KubeConfigPath { get; set; } = "<NOT SET>";

    public static TemporaryDirectory TemporaryDirectory { get; set; } = null!;
}