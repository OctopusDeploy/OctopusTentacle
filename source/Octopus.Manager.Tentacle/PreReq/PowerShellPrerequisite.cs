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
            return CheckPowerShellIsInstalled()
                ? PrerequisiteCheckResult.Successful()
                : PrerequisiteCheckResult.Failed("Windows PowerShell 2.0 or above does not appear to be installed and on the System Path on this machine. Please install Windows PowerShell or add it to the System Path then re-run this setup tool.", helpLink: OutboundLinks.HowDoIInstallPowerShell, helpLinkText: "Download and install Windows PowerShell");
        }

        static bool CheckPowerShellIsInstalled()
        {
            // Despite our old check conforming to Microsoft's recommendations
            // for PS version checking around the 1.0/2.0 era, and extending
            // to detect 3.0, it failed to detect 4. Going the direct route:
            try
            {
                var output = new StringWriter();

                SilentProcessRunner.ExecuteCommand(
                    "powershell.exe",
                    "-NonInteractive -NoProfile -Command \"Write-Output $PSVersionTable.PSVersion\"",
                    ".",
                    output.WriteLine,
                    err =>
                    {
                    });

                var outputText = output.ToString();
                Log.Octopus().Verbose("PowerShell prerequisite check output: " + outputText);
                return outputText.Contains("Major");
            }
            catch
            {
                return false;
            }
        }
    }
}