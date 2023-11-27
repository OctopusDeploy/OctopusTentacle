using System;
using System.IO;
using System.Text;

namespace Octopus.Tentacle.Scripts
{
    public class PowerShell : IShell
    {
        const string EnvPowerShellPath = "PowerShell.exe";
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
                var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                powerShellPath = Path.Combine(systemFolder, @"WindowsPowershell\v1.0\", EnvPowerShellPath);

                if (!File.Exists(powerShellPath))
                    powerShellPath = EnvPowerShellPath;
            }
            catch (Exception)
            {
                powerShellPath = EnvPowerShellPath;
            }

            return powerShellPath;
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