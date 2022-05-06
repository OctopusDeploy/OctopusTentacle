using System;
using System.Text;

namespace Octopus.Shared.Scripts
{
    public class Bash : IShell
    {
        public string GetFullPath()
            => GetFullBashPath();

        public static string GetFullBashPath()
            => "/bin/bash";

        public string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive)
        {
            var commandArguments = new StringBuilder();

            var escapedBootstrapFile = $"\"{bootstrapFile.Replace("'", "''")}\"";
            commandArguments.AppendFormat("{0} {1}", escapedBootstrapFile, string.Join(" ", scriptArguments ?? new string[0]));

            return commandArguments.ToString();
        }
    }
}