using System;
using System.Linq;
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

        public bool StartService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommand("start", serviceName, logFailureAsError);

        public bool RestartService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommand("restart", serviceName, logFailureAsError);

        public bool StopService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommand("stop", serviceName, logFailureAsError);

        public bool EnableService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommand("enable", serviceName, logFailureAsError);

        public bool DisableService(string serviceName, bool logFailureAsError = false)
            => RunServiceCommand("disable", serviceName, logFailureAsError);

        bool RunServiceCommand(string command, string serviceName, bool logFailureAsError)
        {
            // Try without sudo first
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"systemctl {command} {serviceName}\"");
            var result = commandLineInvocation.ExecuteCommand();

            if (result.ExitCode == 0) return true;

            // Check if failure is due to authentication/permission issues
            var needsElevation = result.Errors.Any(e =>
                e.Contains("Interactive authentication required") ||
                e.Contains("Failed to") ||
                e.Contains("Access denied") ||
                e.Contains("Permission denied"));

            // If authentication issue detected, retry with sudo
            if (needsElevation)
            {
                log.Verbose($"Retrying 'systemctl {command} {serviceName}' with sudo");
                var sudoInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo systemctl {command} {serviceName}\"");
                var sudoResult = sudoInvocation.ExecuteCommand();

                if (sudoResult.ExitCode == 0) return true;

                // Log sudo attempt failure
                result = sudoResult;
            }

            void LogErrorOrWarning(string error)
            {
                if (logFailureAsError)
                    log.Error(error);
                else
                    log.Warn(error);
            }

            LogErrorOrWarning($"The command 'systemctl {command} {serviceName}' failed with exit code: {result.ExitCode}");
            foreach (var error in result.Errors)
                LogErrorOrWarning(error);

            return false;
        }
    }
}
