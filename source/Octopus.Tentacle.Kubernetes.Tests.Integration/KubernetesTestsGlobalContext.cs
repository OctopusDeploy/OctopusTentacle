using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class KubernetesTestsGlobalContext : IDisposable
{
    public static KubernetesTestsGlobalContext Instance { get; } = new();

    public TemporaryDirectory TemporaryDirectory { get; }

    public ILogger Logger { get; }

    public string KubeConfigPath { get; set; } = "<unset>";

    public string HelmExePath { get; private set; } = null!;
    public string KubeCtlExePath { get; private set; } = null!;
    public string? TentacleImageAndTag { get; set; }

    KubernetesTestsGlobalContext()
    {
        TemporaryDirectory = new TemporaryDirectory();

        Logger = new SerilogLoggerBuilder().Build();
    }

    public void Dispose()
    {
        TemporaryDirectory.Dispose();
    }

    public void SetToolExePaths(string helmExePath, string kubeCtlPath)
    {
        HelmExePath = helmExePath;
        KubeCtlExePath = kubeCtlPath;
    }
}