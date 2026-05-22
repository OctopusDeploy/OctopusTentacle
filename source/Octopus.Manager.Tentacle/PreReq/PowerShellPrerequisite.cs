using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.PreReq
{
    public class PowerShellPrerequisite : IPrerequisite
    {
        public string StatusMessage => "Checking that Windows PowerShell 2.0 is installed...";

        public async Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            var (isInstalled, commandLineOutput) = await CheckPowerShellIsInstalledAsync(cancellationToken);
            return isInstalled
                ? PrerequisiteCheckResult.Successful()
                : PrerequisiteCheckResult.Failed("Windows PowerShell 2.0 or above does not appear to be installed and on the System Path on this machine. Please install Windows PowerShell or add it to the System Path then re-run this setup tool.",
                    helpLink: "http://g.octopushq.com/HowDoIInstallPowerShell",
                    helpLinkText: "Download and install Windows PowerShell",
                    commandLineOutput: commandLineOutput);
        }

        static async Task<(bool isInstalled, string commandLineOutput)> CheckPowerShellIsInstalledAsync(CancellationToken cancellationToken)
        {
            var stdOut = new StringWriter();
            var stdErr = new StringWriter();

            const string powerShellExe = "powershell.exe";
            const string arguments = "-NonInteractive -NoProfile -Command \"Write-Output $PSVersionTable.PSVersion\"";
            var commandLineOutput = $"{powerShellExe} {arguments}";

            // Despite our old check conforming to Microsoft's recommendations
            // for PS version checking around the 1.0/2.0 era, and extending
            // to detect 3.0, it failed to detect 4. Going the direct route:
            try
            {
                await SilentProcessRunnerExtended.ExecuteCommandAsync(
                    powerShellExe,
                    arguments,
                    ".",
                    stdOut.WriteLine,
                    s => stdErr.WriteLine($"ERR: {s}"),
                    cancel: cancellationToken,
                    abandon: CancellationToken.None);

                var outputText = stdOut.ToString();
                new SystemLog().Verbose("PowerShell prerequisite check output: " + outputText);

                commandLineOutput = $"{commandLineOutput}{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}";

                return (outputText.Contains("Major"), commandLineOutput);
            }
            catch (Exception ex)
            {
                commandLineOutput = $"{commandLineOutput}{Environment.NewLine}{ex}";
                return (false, commandLineOutput);
            }
        }
    }
}
