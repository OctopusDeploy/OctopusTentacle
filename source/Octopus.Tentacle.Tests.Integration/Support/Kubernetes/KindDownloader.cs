using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Serilog;
using PlatformDetection = Octopus.Tentacle.CommonTestUtils.PlatformDetection;

namespace Octopus.Tentacle.Tests.Integration.Support.Kubernetes
{
    public class KindDownloader
    {
        readonly ILogger logger;
        const string LatestKindVersion = "v0.22.0";

        public KindDownloader(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<string> DownloadLatest(string tempDirPath, CancellationToken cancellationToken)
        {
            var downloadUrl = BuildDownloadUrl();

            var executable = "kind";
            if (PlatformDetection.IsRunningOnWindows)
            {
                executable += ".exe";
            }

            var downloadFilePath = Path.Combine(tempDirPath, executable);

            logger.Information("Downloading {DownloadUrl} to {DownloadFilePath}", downloadUrl, downloadFilePath);
            await OctopusPackageDownloader.DownloadPackage(downloadUrl, downloadFilePath, logger, cancellationToken);

            //if this is not running on windows, chmod kind to be executable
            if (!PlatformDetection.IsRunningOnWindows)
            {
                var exitCode = SilentProcessRunner.ExecuteCommand(
                    "chmod",
                    $"+x {downloadFilePath}",
                    tempDirPath,
                    logger.Debug,
                    logger.Information,
                    logger.Error,
                    CancellationToken.None);

                if (exitCode != 0)
                {
                    logger.Error("Error running chmod against kind executable");
                }
            }

            return downloadFilePath;
        }

        static string BuildDownloadUrl()
        {
            var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
            var osName = GetOsName();

            return $"https://github.com/kubernetes-sigs/kind/releases/download/{LatestKindVersion}/kind-{osName}-{architecture}";
        }

        static string GetOsName()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                return "windows";
            }

            if (PlatformDetection.IsRunningOnNix)
            {
                return "linux";
            }

            if (PlatformDetection.IsRunningOnMac)
            {
                return "darwin";
            }

            throw new InvalidOperationException("Unsupported OS");
        }
    }
}