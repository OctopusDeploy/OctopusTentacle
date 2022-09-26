using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public class SystemCtlHelper
    {
        private readonly ISystemLog log;

        public SystemCtlHelper(ISystemLog log)
        {
            this.log = log;
        }

        public bool StartService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("start", serviceName, logFailureAsError);
        }

        public bool RestartService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("restart", serviceName, logFailureAsError);
        }

        public bool StopService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("stop", serviceName, logFailureAsError);
        }

        public bool EnableService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("enable", serviceName, logFailureAsError);
        }

        public bool DisableService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("disable", serviceName, logFailureAsError);
        }

        private bool RunServiceCommand(string command, string serviceName, bool logFailureAsError)
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"systemctl {command} {serviceName}\"");
            var result = commandLineInvocation.ExecuteCommand();

            if (result.ExitCode == 0) return true;

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