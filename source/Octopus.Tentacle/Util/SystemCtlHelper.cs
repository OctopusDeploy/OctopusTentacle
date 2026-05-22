using System.Linq;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public class SystemCtlHelper
    {
        readonly ISystemLog log;

        public SystemCtlHelper(ISystemLog log)
        {
            this.log = log;
        }

        public Task<bool> StartService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommandAsync("start", serviceName, logFailureAsError);

        public Task<bool> RestartService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommandAsync("restart", serviceName, logFailureAsError);

        public Task<bool> StopService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommandAsync("stop", serviceName, logFailureAsError);

        public Task<bool> EnableService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommandAsync("enable", serviceName, logFailureAsError);

        public Task<bool> DisableService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommandAsync("disable", serviceName, logFailureAsError);

        async Task<bool> RunServiceCommandAsync(string command, string serviceName, bool logFailureAsError)
        {
            // Try without sudo first
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"systemctl {command} {serviceName}\"");
            var result = await commandLineInvocation.ExecuteCommandAsync();
            if (result.ExitCode == 0) return true;

            // Check if failure is due to authentication/permission issues
            var needsElevation = result.Errors.Any(e =>
                e.Contains("Interactive authentication required") ||
                e.Contains("Failed to") ||
                e.Contains("Access denied") ||
                e.Contains("Permission denied"));

            var usedSudo = false;
            if (needsElevation)
            {
                log.Info($"Permission denied. Retrying 'systemctl {command} {serviceName}' with sudo...");
                var sudoInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo systemctl {command} {serviceName}\"");
                result = await sudoInvocation.ExecuteCommandAsync();
                if (result.ExitCode == 0) return true;

                usedSudo = true;
            }

            void LogErrorOrWarning(string error)
            {
                if (logFailureAsError)
                    log.Error(error);
                else
                    log.Warn(error);
            }

            LogErrorOrWarning($"The command '{(usedSudo ? "sudo systemctl" : "systemctl")} {command} {serviceName}' failed with exit code: { result.ExitCode}");
            foreach (var error in result.Errors)
                LogErrorOrWarning(error);

            return false;
        }
    }
}
