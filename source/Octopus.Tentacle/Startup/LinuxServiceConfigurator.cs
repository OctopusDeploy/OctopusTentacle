using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public class LinuxServiceConfigurator : IServiceConfigurator
    {
        readonly ISystemLog log;
        readonly SystemCtlHelper systemCtlHelper;

        public LinuxServiceConfigurator(ISystemLog log)
        {
            this.log = log;
            systemCtlHelper = new SystemCtlHelper(log);
        }

        public void ConfigureServiceByInstanceName(string thisServiceName,
            string exePath,
            string instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
        {
            ConfigureService(thisServiceName,
                exePath,
                instance,
                null,
                serviceDescription,
                serviceConfigurationState);
        }

        public void ConfigureServiceByConfigPath(string thisServiceName,
            string exePath,
            string configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
        {
            ConfigureService(thisServiceName,
                exePath,
                null,
                configPath,
                serviceDescription,
                serviceConfigurationState);
        }

        void ConfigureService(string thisServiceName, string exePath, string? instance, string? configPath, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            //Check if system has bash and systemd
            CheckSystemPrerequisites();

            var cleanedInstanceName = SanitizeString(instance ?? thisServiceName);
            var systemdUnitFilePath = $"/etc/systemd/system/{cleanedInstanceName}.service";

            if (serviceConfigurationState.Restart)
                RestartService(cleanedInstanceName);

            if (serviceConfigurationState.Stop)
                StopService(cleanedInstanceName);

            if (serviceConfigurationState.Uninstall)
                UninstallService(cleanedInstanceName, systemdUnitFilePath);

            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] {"network.target"});

            if (serviceConfigurationState.DependOn != null && !string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
                serviceDependencies.Add(serviceConfigurationState.DependOn);

            var userName = serviceConfigurationState.Username ?? "root";
            if (serviceConfigurationState.Install)
                InstallService(cleanedInstanceName,
                    instance,
                    configPath,
                    exePath,
                    serviceDescription,
                    systemdUnitFilePath,
                    userName,
                    serviceDependencies);

            if (serviceConfigurationState.Reconfigure)
                ReconfigureService(cleanedInstanceName,
                    instance,
                    configPath,
                    exePath,
                    serviceDescription,
                    systemdUnitFilePath,
                    userName,
                    serviceDependencies);

            if (serviceConfigurationState.Start)
                StartService(cleanedInstanceName);
        }

        void RestartService(string serviceName)
        {
            log.Info($"Restarting service: {serviceName}");
            if (systemCtlHelper.RestartService(serviceName))
                log.Info("Service has been restarted");
            else
                log.Error("The service could not be restarted");
        }

        void StopService(string serviceName)
        {
            log.Info($"Stopping service: {serviceName}");
            if (systemCtlHelper.StopService(serviceName))
                log.Info("Service stopped");
            else
                log.Error("The service could not be stopped");
        }

        void StartService(string serviceName)
        {
            if (systemCtlHelper.StartService(serviceName, true))
                log.Info($"Service started: {serviceName}");
            else
                log.Error($"Could not start the systemd service: {serviceName}");
        }

        void UninstallService(string instance, string systemdUnitFilePath)
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

        void InstallService(string serviceName, 
            string? instance,
            string? configPath,
            string exePath,
            string serviceDescription,
            string systemdUnitFilePath,
            string userName,
            IEnumerable<string> serviceDependencies)
        {
            try
            {
                WriteUnitFile(systemdUnitFilePath, GenerateSystemdUnitFile(instance, configPath, serviceDescription, exePath, userName, serviceDependencies));
                systemCtlHelper.EnableService(serviceName, true);
                log.Info($"Service installed: {serviceName}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not install the systemd service: {serviceName}");
                throw;
            }
        }

        void ReconfigureService(string serviceName,
            string? instance,
            string? configPath,
            string exePath,
            string serviceDescription,
            string systemdUnitFilePath,
            string userName,
            IEnumerable<string> serviceDependencies)
        {
            try
            {
                log.Info($"Attempting to remove old service: {serviceName}");
                //remove service
                systemCtlHelper.StopService(serviceName);
                systemCtlHelper.DisableService(serviceName);
                File.Delete(systemdUnitFilePath);

                //re-add service
                WriteUnitFile(systemdUnitFilePath, GenerateSystemdUnitFile(instance, configPath, serviceDescription, exePath, userName, serviceDependencies));
                systemCtlHelper.EnableService(serviceName, true);
                log.Info($"Service installed: {serviceName}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not reconfigure the systemd service: {instance}");
                throw;
            }
        }

        void WriteUnitFile(string path, string contents)
        {
            File.WriteAllText(path, contents);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"chmod 644 {path}\"");
            var result = commandLineInvocation.ExecuteCommand();

            if (result.ExitCode == 0) return;

            result.Validate();
        }

        void CheckSystemPrerequisites()
        {
            if (!File.Exists("/bin/bash"))
                throw new ControlledFailureException(
                    "Could not detect bash. bash is required to run tentacle.");

            if (!HaveSudoPrivileges())
                throw new ControlledFailureException(
                    "Requires elevated privileges. Please run command as sudo.");

            if (!IsSystemdInstalled())
                throw new ControlledFailureException(
                    "Could not detect systemd. systemd is required to run Tentacle as a service");
        }

        bool IsSystemdInstalled()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", "-c \"command -v systemctl >/dev/null\"");
            var result = commandLineInvocation.ExecuteCommand();
            return result.ExitCode == 0;
        }

        bool HaveSudoPrivileges()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", "-c \"sudo -vn 2> /dev/null\"");
            var result = commandLineInvocation.ExecuteCommand();
            return result.ExitCode == 0;
        }

        string GenerateSystemdUnitFile(string? instance, 
            string? configPath,
            string serviceDescription, string exePath, string userName, IEnumerable<string> serviceDependencies)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[Unit]");
            stringBuilder.AppendLine($"Description={serviceDescription}");
            stringBuilder.AppendLine($"After={string.Join(" ", serviceDependencies)}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("[Service]");
            stringBuilder.AppendLine("Type=simple");
            stringBuilder.AppendLine($"User={userName}");
            stringBuilder.Append($"ExecStart={exePath} run");
            if (!string.IsNullOrEmpty(instance))
            {
                stringBuilder.Append($" --instance={instance}");
            } 
            else if (!string.IsNullOrEmpty(configPath))
            {
                stringBuilder.Append($" --config={configPath}");
            }
            else
            {
                throw new ControlledFailureException("Either the instance name of configuration path must be provided to configure a service");
            }
            stringBuilder.AppendLine(" --noninteractive");
            stringBuilder.AppendLine("Restart=always");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("[Install]");
            stringBuilder.AppendLine("WantedBy=multi-user.target");

            return stringBuilder.ToString();
        }

        static string SanitizeString(string str)
            => Regex.Replace(str.Replace("/", ""), @"\s+", "-");
    }
}