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

        public bool StartService(string serviceName)
        {
            return RunServiceCommand("start", serviceName);
        }
        
        public bool StopService(string serviceName)
        {
            return RunServiceCommand("stop", serviceName);
        }
        
        public bool EnableService(string serviceName)
        {
            return RunServiceCommand("enable", serviceName);
        }
        
        public bool DisableService(string serviceName)
        {
            return RunServiceCommand("disable", serviceName);
        }

        private bool RunServiceCommand(string command, string serviceName)
        {
            var runner = new CommandLineRunner();
            return runner.Execute(new CommandLineInvocation("/bin/bash", $"-c \"sudo systemctl {command} {serviceName}\""), log);
        }
    }
}