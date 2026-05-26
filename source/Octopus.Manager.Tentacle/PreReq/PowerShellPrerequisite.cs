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

        // We're at the WPF installer prerequisite boundary. IPrerequisite.Check() must return
        // synchronously, so this is the sync-over-async bridge: a one-line wrapper over the
        // private async implementation. Safe because the installer dispatches us on a plain
        // thread-pool worker. No captured SynchronizationContext, so no deadlock.
        // See https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
        public PrerequisiteCheckResult Check()
            => CheckAsync().GetAwaiter().GetResult();

        async Task<PrerequisiteCheckResult> CheckAsync()
        {
            var (installed, commandLineOutput) = await CheckPowerShellIsInstalledAsync();
            return installed
                ? PrerequisiteCheckResult.Successful()
                : PrerequisiteCheckResult.Failed("Windows PowerShell 2.0 or above does not appear to be installed and on the System Path on this machine. Please install Windows PowerShell or add it to the System Path then re-run this setup tool.",
                    helpLink: "http://g.octopushq.com/HowDoIInstallPowerShell",
                    helpLinkText: "Download and install Windows PowerShell",
                    commandLineOutput: commandLineOutput);
        }

        static async Task<(bool installed, string commandLineOutput)> CheckPowerShellIsInstalledAsync()
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
                    cancel: CancellationToken.None);

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
