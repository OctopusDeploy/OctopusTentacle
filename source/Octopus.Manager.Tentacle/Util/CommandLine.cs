﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Util
{
    public class CommandLine
    {
        public static readonly string DefaultSearchPathForTentacleExe = "Tentacle.exe";
        static string[] searchPathsForTentacleExe =
        {
            DefaultSearchPathForTentacleExe
        };
        public static void SetSearchPathsForTentacleExe(params string[] searchPaths)
        {
            searchPathsForTentacleExe = searchPaths;
        }
        public static string PathToTentacleExe()
        {
            return GetPathToExecutable("Tentacle", searchPathsForTentacleExe);
        }

        static string GetPathToExecutable(string executableDescription, params string[] searchPaths)
        {
            var fullPaths = searchPaths.Select(ResolveAssemblyPath);

            var found = fullPaths.FirstOrDefault(File.Exists);
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