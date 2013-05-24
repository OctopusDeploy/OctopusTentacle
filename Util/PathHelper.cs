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
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var root = Uri.UnescapeDataString(uri.Path);
                root = Path.GetDirectoryName(root);
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        public static string ResolveRelativeFilePath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var root = Uri.UnescapeDataString(uri.Path);
                root = Path.GetDirectoryName(root);
                path = Path.Combine(root, path);
            }

            path = Path.GetFullPath(path);

            return path;
        }
    }
}
