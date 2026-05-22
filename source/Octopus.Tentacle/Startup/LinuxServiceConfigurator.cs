using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;
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

        public Task ConfigureServiceByInstanceNameAsync(string thisServiceName,
            string exePath,
            string instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
            => ConfigureServiceAsync(thisServiceName, exePath, instance, null, serviceDescription, serviceConfigurationState);

        public Task ConfigureServiceByConfigPathAsync(string thisServiceName,
            string exePath,
            string configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
            => ConfigureServiceAsync(thisServiceName, exePath, null, configPath, serviceDescription, serviceConfigurationState);

        async Task ConfigureServiceAsync(string thisServiceName, string exePath, string? instance, string? configPath, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            await CheckSystemPrerequisitesAsync();

            var cleanedInstanceName = SanitizeString(instance ?? thisServiceName);
            var systemdUnitFilePath = $"/etc/systemd/system/{cleanedInstanceName}.service";

            if (serviceConfigurationState.Restart)
                await RestartServiceAsync(cleanedInstanceName);

            if (serviceConfigurationState.Stop)
                await StopServiceAsync(cleanedInstanceName);

            if (serviceConfigurationState.Uninstall)
                await UninstallServiceAsync(cleanedInstanceName, systemdUnitFilePath);

            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] {"network.target"});

            if (serviceConfigurationState.DependOn != null && !string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
                serviceDependencies.Add(serviceConfigurationState.DependOn);

            var userName = serviceConfigurationState.Username ?? "root";
            if (serviceConfigurationState.Install)
                await InstallServiceAsync(cleanedInstanceName, instance, configPath, exePath, serviceDescription, systemdUnitFilePath, userName, serviceDependencies);

            if (serviceConfigurationState.Reconfigure)
                await ReconfigureServiceAsync(cleanedInstanceName, instance, configPath, exePath, serviceDescription, systemdUnitFilePath, userName, serviceDependencies);

            if (serviceConfigurationState.Start)
                await StartServiceAsync(cleanedInstanceName);
        }

        async Task RestartServiceAsync(string serviceName)
        {
            log.Info($"Restarting service: {serviceName}");
            if (await systemCtlHelper.RestartService(serviceName))
                log.Info("Service has been restarted");
            else
                log.Error("The service could not be restarted");
        }

        async Task StopServiceAsync(string serviceName)
        {
            log.Info($"Stopping service: {serviceName}");
            if (await systemCtlHelper.StopService(serviceName))
                log.Info("Service stopped");
            else
                log.Error("The service could not be stopped");
        }

        async Task StartServiceAsync(string serviceName)
        {
            if (await systemCtlHelper.StartService(serviceName, true))
                log.Info($"Service started: {serviceName}");
            else
                log.Error($"Could not start the systemd service: {serviceName}");
        }

        async Task UninstallServiceAsync(string instance, string systemdUnitFilePath)
        {
            log.Info($"Removing systemd service: {instance}");
            try
            {
                await systemCtlHelper.StopService(instance);
                await systemCtlHelper.DisableService(instance);
                File.Delete(systemdUnitFilePath);
                log.Info("Service uninstalled");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not remove the systemd service: {instance}");
                throw;
            }
        }

        async Task InstallServiceAsync(string serviceName,
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
                await WriteUnitFileAsync(systemdUnitFilePath, GenerateSystemdUnitFile(instance, configPath, serviceDescription, exePath, userName, serviceDependencies));
                await systemCtlHelper.EnableService(serviceName, true);
                log.Info($"Service installed: {serviceName}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not install the systemd service: {serviceName}");
                throw;
            }
        }

        async Task ReconfigureServiceAsync(string serviceName,
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
                await systemCtlHelper.StopService(serviceName);
                await systemCtlHelper.DisableService(serviceName);
                File.Delete(systemdUnitFilePath);

                await WriteUnitFileAsync(systemdUnitFilePath, GenerateSystemdUnitFile(instance, configPath, serviceDescription, exePath, userName, serviceDependencies));
                await systemCtlHelper.EnableService(serviceName, true);
                log.Info($"Service installed: {serviceName}");
            }
            catch (Exception e)
            {
                log.Error(e, $"Could not reconfigure the systemd service: {instance}");
                throw;
            }
        }

        async Task WriteUnitFileAsync(string path, string contents)
        {
            File.WriteAllText(path, contents);

            var commandLineInvocation = new CommandLineInvocation("/bin/bash", $"-c \"chmod 644 {path}\"");
            var result = await commandLineInvocation.ExecuteCommandAsync();

            if (result.ExitCode == 0) return;

            result.Validate();
        }

        async Task CheckSystemPrerequisitesAsync()
        {
            if (!File.Exists("/bin/bash"))
                throw new ControlledFailureException(
                    "Could not detect bash. bash is required to run tentacle.");

            if (!await HaveSudoPrivilegesAsync())
                throw new ControlledFailureException(
                    "Requires elevated privileges. Please run command as sudo.");

            if (!await IsSystemdInstalledAsync())
                throw new ControlledFailureException(
                    "Could not detect systemd. systemd is required to run Tentacle as a service");
        }

        async Task<bool> IsSystemdInstalledAsync()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", "-c \"command -v systemctl >/dev/null\"");
            var result = await commandLineInvocation.ExecuteCommandAsync();
            return result.ExitCode == 0;
        }

        async Task<bool> HaveSudoPrivilegesAsync()
        {
            var commandLineInvocation = new CommandLineInvocation("/bin/bash", "-c \"sudo -vn 2> /dev/null\"");
            var result = await commandLineInvocation.ExecuteCommandAsync();
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
