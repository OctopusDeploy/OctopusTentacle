using System;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Util;
using OperatingSystem = Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling.OperatingSystem;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;

public class HelmDownloader : ToolDownloader
{
    const string LatestVersion = "v3.19.2";
    public HelmDownloader( ILogger logger)
        : base("helm", logger)
    {
    }

    protected override string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem)
    {
        var architecture = GetArchitectureLabel(processArchitecture);
        var osName = GetOsName(operatingSystem);

        var suffix = operatingSystem is OperatingSystem.Windows ? "zip" : "tar.gz";

        return $"https://get.helm.sh/helm-{LatestVersion}-{osName}-{architecture}.{suffix}";
    }

    static string GetArchitectureLabel(Architecture processArchitecture) => processArchitecture == Architecture.Arm64 ? "arm64" : "amd64";

    protected override string PostDownload(string targetDirectory, string downloadFilePath, Architecture processArchitecture, OperatingSystem operatingSystem)
    {
        var architecture = GetArchitectureLabel(processArchitecture);
        var osName = GetOsName(operatingSystem);

        var extractionDir = Path.Combine(targetDirectory, "extracted");

        //the helm app is zipped, so we need to extract it
        if (operatingSystem is OperatingSystem.Windows)
        {
            //on windows we need to unzip the file
            ZipFile.ExtractToDirectory(downloadFilePath, extractionDir);
        }
        else
        {
            //everything else is tar.gz
            ExtractTarGzip(downloadFilePath, extractionDir);
        }

        //move the extracted helm executable to the root target directory
        var targetFilePath = Path.Combine(targetDirectory, ExecutableName);
        File.Move(Path.Combine(extractionDir,$"{osName}-{architecture}", ExecutableName), targetFilePath);

        //delete the extracted directory
        Directory.Delete(extractionDir,true);
        File.Delete(downloadFilePath);

        return targetFilePath;
    }

    static string GetOsName(OperatingSystem operatingSystem)
        => operatingSystem switch
        {
            OperatingSystem.Windows => "windows",
            OperatingSystem.Nix => "linux",
            OperatingSystem.Mac => "darwin",
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, null)
        };

    void ExtractTarGzip(string gzArchiveName, string destFolder)
    {
        if (!Directory.Exists(destFolder))
        {
            Directory.CreateDirectory(destFolder);
        }

        // We need to use tar directly rather than a C# implementation
        // All C# implementations that we have tried here have not been able to preserve the file permissions stored in the archive
        // In practice, this means that after extraction the executables don't have the executable bit.
        // Falling back to good old fashioned `tar` does the job nicely :)
        using var tmp = new TemporaryDirectory();

        var exitCode = SilentProcessRunner.ExecuteCommand(
            "tar",
            $"xzvf \"{gzArchiveName}\" -C \"{destFolder}\"",
            tmp.DirectoryPath,
            Logger.Debug,
            Logger.Information,
            Logger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            throw new Exception("Error extracting archive");
        }
    }
}