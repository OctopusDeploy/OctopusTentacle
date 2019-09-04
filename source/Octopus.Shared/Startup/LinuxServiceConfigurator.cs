using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class LinuxServiceConfigurator : IServiceConfigurator
    {
        readonly ILog log;
        readonly SystemCtlHelper systemCtlHelper;

        public LinuxServiceConfigurator(ILog log)
        {
            this.log = log;
            systemCtlHelper = new SystemCtlHelper(log);
        }

        public void ConfigureService(string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            //Check if system has bash and systemd
            CheckSystemPrerequisites();

            var cleanedInstanceName = SanitizeString(instance);
            var systemdUnitFilePath = $"/etc/systemd/system/{cleanedInstanceName}.service";

            if (serviceConfigurationState.Restart)
            {
                RestartService(cleanedInstanceName);
            }

            if (serviceConfigurationState.Stop)
            {
                StopService(cleanedInstanceName);
            }

            if (serviceConfigurationState.Uninstall)
            {
                UninstallService(cleanedInstanceName, systemdUnitFilePath);
            }

            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] { "network.target" });

            if (!string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
            {
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }

            if (serviceConfigurationState.Install)
            {
                InstallService(cleanedInstanceName, exePath, serviceDescription, systemdUnitFilePath, serviceDependencies);
            }

            if (serviceConfigurationState.Reconfigure)
            {
                ReconfigureService(cleanedInstanceName, exePath, serviceDescription, systemdUnitFilePath, serviceDependencies);
            }

            if (serviceConfigurationState.Start)
            {
                StartService(cleanedInstanceName);
            }
        }

        private void RestartService(string instance)
        {
            log.Info($"Restarting service: {instance}");
            if (systemCtlHelper.RestartService(instance))
            {
                log.Info("Service has been restarted");
            }
            else
            {
                log.Error("The service could not be restarted");
            }
        }

        private void StopService(string instance)
        {
            log.Info($"Stopping service: {instance}");
            if (systemCtlHelper.StopService(instance))
            {
                log.Info("Service stopped");
            }
            else
            {
                log.Error("The service could not be stopped");
            }
        }

        private void StartService(string instance)
        {
            if (systemCtlHelper.StartService(instance, true))
            {
                log.Info($"Service started: {instance}");
            }
            else
            {
                log.Error($"Could not start the systemd service: {instance}");
            }
        }

        private void UninstallService(string instance, string systemdUnitFilePath)
        {
            log.Info($"Removing systemd service: {instance}");
            try
            {
                systemCtlHelper.StopService(instance);
                systemCtlHelper.DisableService(instance);
                File.Delete(systemdUnitFilePath);
                log.Info("Service uninstalled");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not remove the systemd service: {instance}");
                throw;
            }
        }

        private void InstallService(string instance, string exePath, string serviceDescription, string systemdUnitFilePath, IEnumerable<string> serviceDependencies)
        {
            try
            {
                WriteUnitFile(systemdUnitFilePath, GenerateSystemdUnitFile(instance, serviceDescription, exePath, serviceDependencies));
                systemCtlHelper.EnableService(instance, true);
                log.Info($"Service installed: {instance}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not install the systemd service: {instance}");
                throw;
            }
        }

        private void ReconfigureService(string instance, string exePath, string serviceDescription, string systemdUnitFilePath, IEnumerable<string> serviceDependencies)
        {
            try
            {
                log.Info($"Attempting to remove old service: {instance}");
                //remove service
                systemCtlHelper.StopService(instance);
                systemCtlHelper.DisableService(instance);
                File.Delete(systemdUnitFilePath);

                //re-add service
                WriteUnitFile(systemdUnitFilePath, GenerateSystemdUnitFile(instance, serviceDescription, exePath, serviceDependencies));
                systemCtlHelper.EnableService(instance, true);
                log.Info($"Service installed: {instance}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not reconfigure the systemd service: {instance}");
                throw;
            }
        }

        private void WriteUnitFile(string path, string contents)
        {
            File.WriteAllText(path, contents);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"chmod 666 {path}\"");
            var result = commandLineInvocation.ExecuteCommand();

            if (result.ExitCode == 0) return;

            result.Validate();
        }

        private void CheckSystemPrerequisites()
        {
            if (!File.Exists("/bin/bash"))
            {
                throw new ControlledFailureException(
                    $"Could not detect bash, bash is required to run tentacle.");
            }

            if (!HaveSudoPrivileges())
            {
                throw new ControlledFailureException(
                    $"Requires elevated privileges, please run command as sudo.");
            }

            if (!IsSystemdInstalled())
            {
                throw new ControlledFailureException(
                    $"Could not detect systemd, systemd is required to run Tentacle as a service");
            }
        }

        private bool IsSystemdInstalled()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"command -v systemctl >/dev/null\"");
            var result = commandLineInvocation.ExecuteCommand();
            return result.ExitCode == 0;
        }

        private bool HaveSudoPrivileges()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"sudo -vn 2> /dev/null\"");
            var result = commandLineInvocation.ExecuteCommand();
            return result.ExitCode == 0;
        }

        private string GenerateSystemdUnitFile(string instance, string serviceDescription, string exePath, IEnumerable<string> serviceDependencies)
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

        private static string SanitizeString(string str)
        {
            return Regex.Replace(str.Replace("/", ""), @"\s+", "-");
        }
    }
}
