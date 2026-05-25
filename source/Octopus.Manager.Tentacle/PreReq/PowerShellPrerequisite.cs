using System;
using System.IO;
using System.Threading;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

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

        static bool CheckPowerShellIsInstalled(out string commandLineOutput)
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
                // We're in the WPF installer prerequisite check. IPrerequisite.Check() must return
                // synchronously — there's no async version of the interface — so we block on the async
                // call with .GetAwaiter().GetResult().
                // This is safe because we're on a plain thread-pool worker. The risk with blocking on
                // async is a deadlock: if the async work needs to resume on the same thread that's
                // blocked waiting for it, neither can make progress. Thread-pool workers don't have
                // that constraint — when the async work finishes it can pick up on any free thread,
                // not specifically this one, so the block resolves normally.
                SilentProcessRunnerExtended.ExecuteCommandAsync(
                    powerShellExe,
                    arguments,
                    ".",
                    stdOut.WriteLine,
                    s => stdErr.WriteLine($"ERR: {s}"),
                    cancel: CancellationToken.None).GetAwaiter().GetResult();

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
