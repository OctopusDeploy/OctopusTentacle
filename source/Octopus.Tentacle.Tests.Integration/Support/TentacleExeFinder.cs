using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TentacleExeFinder
    {

        public static string FindTentacleExe()
        {
            var assemblyDir = new DirectoryInfo(Path.GetDirectoryName(typeof(TentacleExeFinder).Assembly.Location)!);
            var tentacleExe = Path.Combine(assemblyDir.FullName, "Tentacle");
            

            // We don't have access to any teamcity environment variables so instead rely on the path. 
            if (assemblyDir.FullName.Contains("TeamCity"))
            {
                // Example current directory of assembly.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/integrationtests/net6.0/linux-x64
                // Desired path to tentacle.
                // /opt/TeamCity/BuildAgent/work/639265b01610d682/build/outputs/tentaclereal/tentacle/Tentacle

                tentacleExe = Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, "tentaclereal", "tentacle", "Tentacle");
                return AddExeExtension(tentacleExe);
            }
            
            // Try to use tentacle from the Tentacle project
            var tentacleProjectBinDir = new DirectoryInfo(Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, "Octopus.Tentacle", assemblyDir.Parent.Name, assemblyDir.Name));
            if (tentacleProjectBinDir.Exists)
            {
                return AddExeExtension(Path.Combine(tentacleProjectBinDir.FullName, "Tentacle"));
            }
            
            var tentaclePublishBinDir = new DirectoryInfo(Path.Combine(assemblyDir.Parent.Parent.Parent.FullName, "Tentacle", assemblyDir.Parent.Name, assemblyDir.Name));
            if (tentaclePublishBinDir.Exists)
            {
                return AddExeExtension(Path.Combine(tentaclePublishBinDir.FullName, "Tentacle"));
            }

            throw new Exception("Could not determine where to look for Tentacle Exe started searching from: " + assemblyDir.FullName);
        }

        public static string AddExeExtension(string path)
        {
            if (PlatformDetection.IsRunningOnWindows) path += ".exe";
            return path;
        }
    }
}