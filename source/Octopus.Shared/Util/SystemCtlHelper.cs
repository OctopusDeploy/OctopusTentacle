using System;
using System.Linq;
using Octopus.Diagnostics;

namespace Octopus.Shared.Util
{
    public class SystemCtlHelper
    {
        readonly ILog log;
        
        public SystemCtlHelper(ILog log)
        {
            this.log = log;
        }

        public bool StartService(string serviceName, bool logFailureAsError = false)
        {
            return RunServiceCommand("start", serviceName, logFailureAsError);
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
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo -n systemctl {command} {serviceName}\"");
            var result = commandLineInvocation.ExecuteCommand();
            
            if (result.ExitCode == 0) return true;

            if (result.Errors.Any(s => s.Contains("sudo: a password is required")))
            {
                throw new ControlledFailureException(
                    $"Requires elevated privileges, please run command as sudo.");
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
            {
                LogErrorOrWarning(error);
            }

            return false;
        }
    }
}