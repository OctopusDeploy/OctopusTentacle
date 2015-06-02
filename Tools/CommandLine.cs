using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tools
{
    public class CommandLine
    {
        public static string PathToOctopusServerExe()
        {
            return GetPathToExecutable(
                "Octopus Server",
                "..\\..\\Octopus.Server\\bin\\Octopus.Server.exe",
                "Octopus.Server.exe");
        }

        public static string PathToOctopusMigratorExe()
        {
            return GetPathToExecutable(
                "Octopus Migrator",
                "..\\..\\Octopus.Migrator\\bin\\Octopus.Migrator.exe",
                "Octopus.Migrator.exe");
        }

        public static string PathToTentacleExe()
        {
            return GetPathToExecutable(
                "Tentacle",
                "..\\..\\Octopus.Tentacle\\bin\\Tentacle.exe",
                "Tentacle.exe");
        }

        static string GetPathToExecutable(string executableDescription, params string[] searchPaths)
        {
            var fullPaths = searchPaths.Select(ResolveAssemblyPath);

            var found = fullPaths.FirstOrDefault(p => File.Exists(p));
            if (found == null)
            {
                throw new FileNotFoundException("The " + executableDescription + " executable was not found at any of the following paths: " + Environment.NewLine + string.Join(Environment.NewLine, fullPaths));
            }

            return found;
        }

        static string ResolveAssemblyPath(string pathToInstaller)
        {
            var path = Assembly.GetExecutingAssembly().FullLocalPath();
            path = Path.GetDirectoryName(path);
            path = Path.Combine(path, pathToInstaller);
            path = Path.GetFullPath(path);
            return path;
        }
    }
}