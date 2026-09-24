using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class KubernetesTestsGlobalContext : IDisposable
{
    public static KubernetesTestsGlobalContext Instance { get; } = new();
    
    public TemporaryDirectory TemporaryDirectory { get; }
    
    public ILogger Logger { get; }

    public string KubeConfigPath { get; set; } = "<unset>";

    string? helmExePath;
    string? kubeCtlExePath;

    public string HelmExePath => helmExePath ?? throw new InvalidOperationException($"{nameof(HelmExePath)} has not been set. {nameof(SetToolExePaths)} must be called first.");
    public string KubeCtlExePath => kubeCtlExePath ?? throw new InvalidOperationException($"{nameof(KubeCtlExePath)} has not been set. {nameof(SetToolExePaths)} must be called first.");
    public string? TentacleImageAndTag { get; set; }
    
    internal KubernetesTestsGlobalContext(ILogger logger)
    {
        TemporaryDirectory = new TemporaryDirectory();

        Logger = logger;
    }

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
        this.helmExePath = helmExePath;
        kubeCtlExePath = kubeCtlPath;
    }
}