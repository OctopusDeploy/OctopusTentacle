using System;
using System.IO;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.PreReq
{
    public class PowerShellPrerequisite : IPrerequisite
    {
        public string StatusMessage => "Checking that Windows PowerShell 2.0 is installed...";

        public PrerequisiteCheckResult Check()
        {
            return CheckPowerShellIsInstalled(out var commandLineOutput)
                ? PrerequisiteCheckResult.Successful()
                : PrerequisiteCheckResult.Failed("Windows PowerShell 2.0 or above does not appear to be installed and on the System Path on this machine. Please install Windows PowerShell or add it to the System Path then re-run this setup tool.",
                    helpLink: "http://g.octopushq.com/HowDoIInstallPowerShell",
                    helpLinkText: "Download and install Windows PowerShell",
                    commandLineOutput: commandLineOutput);
        }

        private static bool CheckPowerShellIsInstalled(out string commandLineOutput)
        {
            var stdOut = new StringWriter();
            var stdErr = new StringWriter();

            const string powerShellExe = "powershell.exe";
            const string arguments = "-NonInteractive -NoProfile -Command \"Write-Output $PSVersionTable.PSVersion\"";
            commandLineOutput = $"{powerShellExe} {arguments}";

            // Despite our old check conforming to Microsoft's recommendations
            // for PS version checking around the 1.0/2.0 era, and extending
            // to detect 3.0, it failed to detect 4. Going the direct route:
            try
            {
                SilentProcessRunner.ExecuteCommand(
                    powerShellExe,
                    arguments,
                    ".",
                    stdOut.WriteLine,
                    s => stdErr.WriteLine($"ERR: {s}"));

                var outputText = stdOut.ToString();
                new SystemLog().Verbose("PowerShell prerequisite check output: " + outputText);

                commandLineOutput = $"{commandLineOutput}{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}";

                return outputText.Contains("Major");
            }
            catch (Exception ex)
            {
                commandLineOutput = $"{commandLineOutput}{Environment.NewLine}{ex}";
                return false;
            }
        }
    }
}