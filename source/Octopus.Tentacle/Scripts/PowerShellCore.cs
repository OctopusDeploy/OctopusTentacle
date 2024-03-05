using System;
using System.IO;
using System.Text;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class PowerShellCore : IShell
    {
        const string EnvPowerShellPath = "pwsh";
        static string? powerShellPath;

        public string Name => nameof(PowerShell);

        public string GetFullPath()
            => GetFullPowerShellPath();

        public static string GetFullPowerShellPath()
        {
            if (powerShellPath != null)
                return powerShellPath;

            try
            {
                powerShellPath = SearchPathForPathExecutable(EnvPowerShellPath);

                if (powerShellPath == null || !File.Exists(powerShellPath))
                    powerShellPath = EnvPowerShellPath;
            }
            catch (Exception)
            {
                powerShellPath = EnvPowerShellPath;
            }

            return powerShellPath;
        }

        static string? SearchPathForPathExecutable(string pathExecutable)
        {
            if (File.Exists(pathExecutable))
                return Path.GetFullPath(pathExecutable);

            var environmentVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(environmentVariable))
                return null;

            foreach (var path in environmentVariable.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, pathExecutable);
                if (string.IsNullOrEmpty(Path.GetExtension(fullPath)) && PlatformDetection.IsRunningOnWindows)
                    fullPath += ".exe";
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
        
        public string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive)
        {
            var commandArguments = new StringBuilder();

            // This option is provided for debugging purposes; sometimes
            // PowerShell fails when in non-interactive mode without indicating why.
            if (!allowInteractive)
                commandArguments.Append("-NonInteractive ");
            // Don't load the user profile when we run powershell. Calamari loads the
            // profile when it runs PS unless ExecuteWithoutProfile is set. For example
            // when we call CalamariRunScript.ps1 or CalamariRunAzurePowerShell.ps1 here.
            commandArguments.Append("-NoProfile ");
            commandArguments.Append("-NoLogo ");
            commandArguments.Append("-ExecutionPolicy Unrestricted ");
            var escapedBootstrapFile = bootstrapFile.Replace("'", "''");
            commandArguments.AppendFormat("-Command \"$ErrorActionPreference = 'Stop'; . {{. '{0}' {1}; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}}\"",
                escapedBootstrapFile,
                string.Join(" ", scriptArguments ?? new string[0]));
            return commandArguments.ToString();
        }
    }
}
