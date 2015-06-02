using System;
using System.IO;
using System.Text;

namespace Octopus.Shared.Scripts
{
    public class PowerShell
    {
        const string EnvPowerShellPath = "PowerShell.exe";
        static string powerShellPath;

        public static string GetFullPath()
        {
            if (powerShellPath != null)
            {
                return powerShellPath;
            }

            try
            {
                var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                powerShellPath = Path.Combine(systemFolder, @"WindowsPowershell\v1.0\", EnvPowerShellPath);

                if (!File.Exists(powerShellPath))
                {
                    powerShellPath = EnvPowerShellPath;
                }
            }
            catch (Exception)
            {
                powerShellPath = EnvPowerShellPath;
            }

            return powerShellPath;
        }

        public static string FormatCommandArguments(string bootstrapFile, bool allowInteractive)
        {
            var commandArguments = new StringBuilder();

            // This option is provided for debugging purposes; sometimes
            // PowerShell fails when in non-interactive mode without indicating why.
            if (!allowInteractive)
                commandArguments.Append("-NonInteractive ");

            commandArguments.Append("-NoLogo ");
            commandArguments.Append("-ExecutionPolicy Unrestricted ");
            var escapedBootstrapFile = bootstrapFile.Replace("'", "''");
            commandArguments.AppendFormat("-Command \"$ErrorActionPreference = 'Stop'; . {{. '{0}'; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}}\"", escapedBootstrapFile);
            return commandArguments.ToString();
        }
    }
}