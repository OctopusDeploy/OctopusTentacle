using System;
using System.IO;
using System.Reflection;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tools
{
    public class OctoDiff
    {
        static string octoDiffPath;

        public static string GetFullPath()
        {
            if (octoDiffPath != null)
            {
                return octoDiffPath;
            }

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullLocalPath());
            octoDiffPath = Path.Combine(path, "Octodiff.exe");

            if (!File.Exists(octoDiffPath))
            {
                throw new ApplicationException(String.Format("Unable to find Octodiff.exe in {0}.", path));
            }

            return octoDiffPath;
        }
    }
}
