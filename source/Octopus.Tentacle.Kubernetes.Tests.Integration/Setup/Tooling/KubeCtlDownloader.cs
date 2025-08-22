using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;

public class KubeCtlDownloader : ToolDownloader
{
    public const string LatestKubeCtlVersion = "v1.32.5";
    
    public KubeCtlDownloader(ILogger logger) 
        : base("kubectl", logger)
    { }

    protected override string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem)
    {
        var architecture = processArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
        var osName = GetOsName(operatingSystem);

        var extension = operatingSystem is OperatingSystem.Windows
            ? ".exe"
            : null;

        return $"https://dl.k8s.io/release/{LatestKubeCtlVersion}/bin/{osName}/{architecture}/kubectl{extension}";        
    }

    static string GetOsName(OperatingSystem operatingSystem)
        => operatingSystem switch
        {
            OperatingSystem.Windows => "windows",
            OperatingSystem.Nix => "linux",
            OperatingSystem.Mac => "darwin",
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, null)
        };
}