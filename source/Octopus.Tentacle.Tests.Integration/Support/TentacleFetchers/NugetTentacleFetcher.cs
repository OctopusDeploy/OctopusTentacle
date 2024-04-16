using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils;
using Serilog;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers
{
    /// <summary>
    /// Downloads tentacle from the cross platform nuget package
    /// Earliest version supported: 6.0.174
    /// </summary>
    public class NugetTentacleFetcher : ITentacleFetcher
    {
        private ILogger logger;

        public NugetTentacleFetcher(ILogger logger)
        {
            this.logger = logger;
        }

        static string DownloadUrlForVersion(string versionString) => $"https://f.feedz.io/octopus-deploy/dependencies/packages/Octopus.Tentacle.CrossPlatformBundle/{versionString}/download";

        public async Task<string> GetTentacleVersion(string downloadPath, Version version, TentacleRuntime runtime, CancellationToken cancellationToken)
        {
            return await DownloadAndExtractFromUrl(downloadPath, runtime, DownloadUrlForVersion(version.ToString()));
        }

        async Task<string> DownloadAndExtractFromUrl(string directoryPath, TentacleRuntime runtime, string url)
        {
            await Task.CompletedTask;
            var downloadFilePath = Path.Combine(directoryPath, Guid.NewGuid().ToString("N"));

            logger.Information($"Downloading {url} to {downloadFilePath}");
            await OctopusPackageDownloader.DownloadPackage(url, downloadFilePath, logger);

            var extractionDirectory = Path.Combine(directoryPath, "extracted");

            ZipFile.ExtractToDirectory(downloadFilePath, extractionDirectory);

            // This is the path to the runtime-specific artifact
            var tentacleArtifacts = TentacleArtifactNames(runtime)
                .Select(name => Path.Combine(extractionDirectory, name))
                .ToArray();
            var tentacleArtifact = tentacleArtifacts.FirstOrDefault(File.Exists);
            if (tentacleArtifact == null)
            {
                var artifactNamesList = string.Join(Environment.NewLine, tentacleArtifacts
                    .Select(Path.GetFileName)
                    .Select(a => $"- {a}")
                );
                throw new FileNotFoundException($"Could not find any of the expected tentacle artifacts in {extractionDirectory}.{Environment.NewLine}{artifactNamesList}");
            }
            logger.Information($"Extracting tentacle from {tentacleArtifact}");

            var tentacleFolder = Path.Combine(directoryPath, "tentacle");
            if (tentacleArtifact.EndsWith(".tar.gz"))
            {
                LinuxTentacleFetcher.ExtractTarGzip(tentacleArtifact, tentacleFolder, logger);
            }
            else
            {
                ZipFile.ExtractToDirectory(tentacleArtifact, tentacleFolder);
            }

            return Path.Combine(tentacleFolder, "tentacle", "Tentacle");
        }

        public string[] TentacleArtifactNames(TentacleRuntime runtime)
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                var net48ArtifactNames = new[] {"tentacle-net48-win.zip"};
                var net60ArtifactNames = Architectures()
                    .Select(a => $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-win-{a}.zip")
                    .ToArray();

                var names = runtime switch
                {
                    TentacleRuntime.DotNet6 => net60ArtifactNames,
                    TentacleRuntime.Framework48 => net48ArtifactNames,
                    _ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, null)
                };

                return names;
            }

            if (PlatformDetection.IsRunningOnMac)
            {
                return Architectures()
                    .Select(a => $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-osx-{a}.tar.gz")
                    .ToArray();
            }

            if (PlatformDetection.IsRunningOnNix)
            {
                return Architectures()
                    .Select(a => $"tentacle-{RuntimeDetection.GetCurrentRuntime()}-linux-{a}.tar.gz")
                    .ToArray();
            }

            throw new Exception("Wow this is one fancy OS that tentacle probably can't run on");
        }

        string[] Architectures()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                if (PlatformDetection.IsRunningOnMac)
                {
                    // Mac automatically emulates for x64 processes on ARM and the ARM version of
                    // Tentacle from the bundle does not currently run.
                    return new[]
                    {
                        Architecture.X64.ToString().ToLower()
                    };
                }

                if (PlatformDetection.IsRunningOnWindows)
                {
                    // Windows automatically emulates for x64 processes on ARM, so we can fallback
                    // to the x64 Tentacle if an ARM version is unavailable.
                    return new[]
                    {
                        RuntimeInformation.ProcessArchitecture.ToString().ToLower(),
                        Architecture.X64.ToString().ToLower()
                    };
                }
            }
            return new []
            {
                RuntimeInformation.ProcessArchitecture.ToString().ToLower()
            };
        }
    }
}
