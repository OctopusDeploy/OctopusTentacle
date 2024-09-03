using System;
using System.IO;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleExeFinder
    {
        public static string FindTentacleExe()
        {
            return FindTentacleExe(DefaultTentacleRuntime.Value);
        }

        public static string FindTentacleExe(TentacleRuntime version)
        {
            var assemblyDir = new DirectoryInfo(Path.GetDirectoryName(typeof(TentacleExeFinder).Assembly.Location)!);

            // We don't have access to any teamcity environment variables so instead rely on the path. 
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                // Example current directory of assembly.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/integrationtests/net6.0/linux-x64
                // Desired path to tentacle.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/tentaclereal/tentacle/Tentacle

                const string net48ArtifactDir = "tentaclereal-net48";
                const string net80ArtifactDir = "tentaclereal-net8.0";
                
                string artifactDir =
                    version switch
                    {
                        TentacleRuntime.Framework48 => net48ArtifactDir,
                        TentacleRuntime.DotNet8 => net80ArtifactDir,
                        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
                    };

                var tentacleArtifactPath = Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, artifactDir, "tentacle");
                return GetExecutablePath(tentacleArtifactPath);
            }

            string runtimeDir =
                version switch
                {
                    TentacleRuntime.Framework48 => "net48",
                    TentacleRuntime.DotNet8 => "net8.0",
                    _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
                };

            // Try to use tentacle from the Tentacle project
            var tentacleProjectBinDir = new DirectoryInfo(Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, "Octopus.Tentacle", assemblyDir.Parent.Name, runtimeDir));
            if (tentacleProjectBinDir.Exists)
            {
                return GetExecutablePath(tentacleProjectBinDir.FullName);
            }

            var tentaclePublishBinDir = new DirectoryInfo(Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, "Tentacle", assemblyDir.Parent.Name, runtimeDir));
            if (tentaclePublishBinDir.Exists)
            {
                return GetExecutablePath(tentaclePublishBinDir.FullName);
            }

            throw new Exception("Could not determine where to look for Tentacle Exe started searching from: " + assemblyDir.FullName);
        }

        public static string GetExecutablePath(string tentacleDir)
        {
            var executableName = "Tentacle";
            if (PlatformDetection.IsRunningOnWindows) executableName += ".exe";
            return Path.Combine(tentacleDir, executableName);
        }
    }
}
