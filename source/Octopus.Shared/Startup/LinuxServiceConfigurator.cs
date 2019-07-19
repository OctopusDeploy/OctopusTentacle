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
            var systemCtlHelper = new SystemCtlHelper(log);
            
            if (serviceConfigurationState.Stop)
            {
                systemCtlHelper.StopService(instance);
            }

            if (serviceConfigurationState.Uninstall)
            {
                systemCtlHelper.StopService(instance);
                systemCtlHelper.DisableService(instance);
                File.Delete(SystemdUnitFilePath);
            }
            
            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] { "network.target" });

            if (!string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
            {
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }
            
            if (serviceConfigurationState.Install)
            {
                WriteUnitFile(SystemdUnitFilePath, GenerateSystemdUnitFile(serviceDependencies));
                systemCtlHelper.EnableService(instance);
            }
            
            if (serviceConfigurationState.Reconfigure)
            {
                //remove service
                systemCtlHelper.StopService(instance);
                systemCtlHelper.DisableService(instance);
                File.Delete(SystemdUnitFilePath);
                
                //re-add service
                WriteUnitFile(SystemdUnitFilePath, GenerateSystemdUnitFile(serviceDependencies));
                systemCtlHelper.EnableService(instance);
            }
            
            if (serviceConfigurationState.Start)
            {
                systemCtlHelper.StartService(instance);
            }
        }

        private void WriteUnitFile(string path, string contents)
        {
            File.WriteAllText(path, contents);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo -n chmod 666 {path}\"");
            var result = commandLineInvocation.ExecuteCommand();
            result.Validate();
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
