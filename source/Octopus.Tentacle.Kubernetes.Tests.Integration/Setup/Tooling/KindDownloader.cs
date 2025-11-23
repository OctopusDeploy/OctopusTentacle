using System;
using System.Runtime.InteropServices;
using OperatingSystem = Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling.OperatingSystem;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling
{
    public class KindDownloader : ToolDownloader
    {
        const string LatestKindVersion = "v0.30.0";

        public KindDownloader(ILogger logger)
            : base("kind", logger)
        {
        }

        protected override string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem)
        {
            var architecture = processArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
            var osName = GetOsName(operatingSystem);

            return $"https://github.com/kubernetes-sigs/kind/releases/download/{LatestKindVersion}/kind-{osName}-{architecture}";
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
}