using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class LinuxServiceConfigurator : IServiceConfigurator
    {
        readonly ILog log;
        readonly string thisServiceName;
        readonly string exePath;
        readonly string instance;
        readonly string serviceDescription;
        readonly ServiceConfigurationState serviceConfigurationState;

        private string SystemdUnitFilePath => $"/etc/systemd/system/{instance}.service";
        
        public LinuxServiceConfigurator(ILog log, string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            this.log = log;
            this.thisServiceName = thisServiceName;
            this.exePath = exePath;
            this.instance = instance;
            this.serviceDescription = serviceDescription;
            this.serviceConfigurationState = serviceConfigurationState;
        }

        public void ConfigureService()
        {
            //Check if systemd is being used, if not bail out.
            if (!IsSystemdInstalled())
            {
                log.Error("Could not detect systemd.");
                return;
            }
            
            var systemCtlHelper = new SystemCtlHelper(log);
            
            if (serviceConfigurationState.Stop)
            {
                log.Info("Stopping service...");
                if (systemCtlHelper.StopService(instance))
                {
                    log.Error("The service could not be stopped");
                }
                else
                {
                    log.Info("Service stopped");
                }
                
            }

            if (serviceConfigurationState.Uninstall)
            {
                log.Info("Removing systemd service...");
                try
                {
                    systemCtlHelper.StopService(instance);
                    systemCtlHelper.DisableService(instance);
                    File.Delete(SystemdUnitFilePath);
                    log.Info("Service uninstalled");
                }
                catch (Exception e)
                {
                    log.Error(e, $"Could not remove the systemd service: {instance}");
                }
            }
            
            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] { "network.target" });

            if (!string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
            {
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }
            
            if (serviceConfigurationState.Install)
            {
                try
                {
                    WriteUnitFile(SystemdUnitFilePath, GenerateSystemdUnitFile(serviceDependencies));
                    systemCtlHelper.EnableService(instance, true);
                    log.Info("Service installed");
                }
                catch (Exception e)
                {
                    log.Error(e, $"Could not install the systemd service: {instance}");
                }
            }
            
            if (serviceConfigurationState.Reconfigure)
            {
                try
                {
                    log.Info("Attempting to remove old service");
                    //remove service
                    systemCtlHelper.StopService(instance);
                    systemCtlHelper.DisableService(instance);
                    File.Delete(SystemdUnitFilePath);
                    
                    //re-add service
                    WriteUnitFile(SystemdUnitFilePath, GenerateSystemdUnitFile(serviceDependencies));
                    systemCtlHelper.EnableService(instance, true);
                    log.Info("Service installed");
                }
                catch (Exception e)
                {
                    log.Error(e, $"Could not reconfigure the systemd service: {instance}");
                }
            }
            
            if (serviceConfigurationState.Start)
            {
                if (systemCtlHelper.StartService(instance, true))
                {
                    log.Info("Service started");
                }
                else
                {
                    log.Error($"Could not start the systemd service: {instance}");
                }
            }
        }

        private void WriteUnitFile(string path, string contents)
        {
            File.WriteAllText(path, contents);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo -n chmod 666 {path}\"");
            var result = commandLineInvocation.ExecuteCommand();
            result.Validate();
        }

        private bool IsSystemdInstalled()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"command -v systemctl >/dev/null\"");
            var result = commandLineInvocation.ExecuteCommand();
            return result.ExitCode == 0;
        }

        private string GenerateSystemdUnitFile(IEnumerable<string> serviceDependencies)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[Unit]");
            stringBuilder.AppendLine($"Description={serviceDescription}");
            stringBuilder.AppendLine($"After={string.Join(" ", serviceDependencies)}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("[Service]");
            stringBuilder.AppendLine("Type=simple");
            stringBuilder.AppendLine("User=root");
            stringBuilder.AppendLine($"ExecStart={exePath} run --instance={instance} --noninteractive");
            stringBuilder.AppendLine("Restart=always");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("[Install]");
            stringBuilder.AppendLine("WantedBy=multi-user.target");
            
            return stringBuilder.ToString();
        }
    }
}
