using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tools
{
    public class OctoDiff
    {
        static string octoDiffPath;
        const string EnvOctoDiffPath = "Octodiff.exe";

        public static string GetFullPath()
        {
            if (octoDiffPath != null)
            {
                return octoDiffPath;
            }

            try
            {
                var path = Assembly.GetExecutingAssembly().FullLocalPath();
                octoDiffPath = Path.Combine(path, EnvOctoDiffPath);

                if (!File.Exists(octoDiffPath))
                {
                    octoDiffPath = EnvOctoDiffPath;
                }
            }
            catch (Exception)
            {
                octoDiffPath = EnvOctoDiffPath;
            }

            return octoDiffPath;
        }

        public static string FormatCommandArguments(string command, params object[] args)
        {
            var commandArguments = new StringBuilder();

            commandArguments.Append(command);
            commandArguments.Append(" ");
            commandArguments.Append(String.Join(" ", args));

            return commandArguments.ToString();
        }

    }
}
