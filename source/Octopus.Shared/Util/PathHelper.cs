using System;
using System.IO;
using System.Reflection;

namespace Octopus.Shared.Util
{
    public static class PathHelper
    {
        public static string ResolveRelativeDirectoryPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase ?? throw new Exception("CodeBase not found for executing assembly");
                var uri = new UriBuilder(codeBase);
                var root = Uri.UnescapeDataString(uri.Path);
                root = Path.GetDirectoryName(root) ?? throw new Exception("Directory for executing assembly not found");
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static string ResolveRelativeFilePath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase ?? throw new Exception("CodeBase not found for executing assembly");
                var uri = new UriBuilder(codeBase);
                var root = Uri.UnescapeDataString(uri.Path);
                root = Path.GetDirectoryName(root) ?? throw new Exception("Directory for executing assembly not found");
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);

            return path;
        }

        public static string GetPathWithoutExtension(string path)
        {
            var containingFolder = Path.GetDirectoryName(path);
            var fileWithoutExtension = Path.GetFileNameWithoutExtension(path);

            return !string.IsNullOrEmpty(containingFolder) ? Path.Combine(containingFolder, fileWithoutExtension) : fileWithoutExtension;
        }
    }
}