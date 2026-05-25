using System;
using System.Runtime.InteropServices;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Util;
using PlatformDetection = Octopus.Tentacle.CommonTestUtils.PlatformDetection;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;

public abstract class ToolDownloader : IToolDownloader
{
    readonly OperatingSystem os;

    protected ILogger Logger { get; }
    protected string ExecutableName { get; }

    protected ToolDownloader(string executableName, ILogger logger)
    {
        ExecutableName = executableName;
        Logger = logger;

        os = GetOperationSystem();

        //we assume that windows always has .exe suffixed
        if (os is OperatingSystem.Windows)
        {
            ExecutableName += ".exe";
        }
    }

    public async Task<string> Download(string targetDirectory, CancellationToken cancellationToken)
    {
        var downloadUrl = BuildDownloadUrl(RuntimeInformation.ProcessArchitecture, os);

        //we download to a random file name
        var downloadFilePath = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N"));

        Logger.Information("Downloading {DownloadUrl} to {DownloadFilePath}", downloadUrl, downloadFilePath);
        await OctopusPackageDownloader.DownloadPackage(downloadUrl, downloadFilePath, Logger, cancellationToken);

        downloadFilePath = await PostDownload(targetDirectory, downloadFilePath, RuntimeInformation.ProcessArchitecture, os);

        //if this is not running on windows, chmod the tool to be executable
        if (os is not OperatingSystem.Windows)
        {
            var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
                "chmod",
                $"+x \"{downloadFilePath}\"",
                targetDirectory,
                Logger.Debug,
                Logger.Information,
                Logger.Error,
                cancel: CancellationToken.None);

            if (exitCode != 0)
            {
                Logger.Error("Error running chmod against executable {ExecutablePath}", downloadFilePath);
            }
        }

        return downloadFilePath;
    }

    protected abstract string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem);

    protected virtual Task<string> PostDownload(string downloadDirectory, string downloadFilePath, Architecture processArchitecture, OperatingSystem operatingSystem)
    {
        var targetFilename = Path.Combine(downloadDirectory, ExecutableName);
        File.Move(downloadFilePath, targetFilename);

        return Task.FromResult(targetFilename);
    }

    static OperatingSystem GetOperationSystem()
    {
        if (PlatformDetection.IsRunningOnWindows)
        {
            return OperatingSystem.Windows;
        }

        if (PlatformDetection.IsRunningOnNix)
        {
            return OperatingSystem.Nix;
        }

        if (PlatformDetection.IsRunningOnMac)
        {
            return OperatingSystem.Mac;
        }

        throw new InvalidOperationException("Unsupported OS");
    }
}

public enum OperatingSystem
{
    Windows,
    Nix,
    Mac
}