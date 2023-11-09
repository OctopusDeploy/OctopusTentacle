using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;
using Polly;

namespace Octopus.Tentacle.Startup
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class WindowsServiceConfigurator : IServiceConfigurator
    {
        readonly ISystemLog log;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;

        public WindowsServiceConfigurator(
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger,
            IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker)
        {
            this.log = log;
            this.logFileOnlyLogger = logFileOnlyLogger;
            this.windowsLocalAdminRightsChecker = windowsLocalAdminRightsChecker;
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

        void ConfigureService(string thisServiceName,
            string exePath,
            string? instance,
            string? configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
        {
            windowsLocalAdminRightsChecker.AssertIsRunningElevated();
            var services = ServiceController.GetServices();
            var controller = services.FirstOrDefault(s => s.ServiceName == thisServiceName);

            if (serviceConfigurationState.Restart)
            {
                RestartService(thisServiceName, controller);
            }

            if (serviceConfigurationState.Stop)
            {
                StopService(thisServiceName, controller);
            }

            if (serviceConfigurationState.Uninstall)
            {
                UninstallService(thisServiceName, controller);
            }

            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] { "LanmanWorkstation", "TCPIP" });

            if (serviceConfigurationState.DependOn != null && !string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
            {
                if (services.None(x => string.Equals(x.ServiceName, serviceConfigurationState.DependOn, StringComparison.OrdinalIgnoreCase)))
                    throw new ControlledFailureException($"Unable to set dependency on service '{serviceConfigurationState.DependOn}' as no service was found with that name.");
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }

            if (serviceConfigurationState.Install)
            {
                controller = InstallService(thisServiceName, exePath, instance, configPath,
                    serviceDescription, serviceConfigurationState, controller, serviceDependencies);
            }

            if (serviceConfigurationState.Reconfigure)
            {
                ReconfigureService(thisServiceName, exePath, instance, configPath, serviceDescription, serviceDependencies);
            }

            if ((serviceConfigurationState.Install || serviceConfigurationState.Reconfigure) && !string.IsNullOrWhiteSpace(serviceConfigurationState.Username))
            {
                ConfigureCredentialsForService(thisServiceName, serviceConfigurationState);
            }

            if (serviceConfigurationState.Start)
            {
                StartService(thisServiceName, controller);
            }
        }

        void ConfigureCredentialsForService(string thisServiceName, ServiceConfigurationState serviceConfigurationState)
        {
            if (!string.IsNullOrWhiteSpace(serviceConfigurationState.Password))
            {
                log.Info("Granting log on as a service right to " + serviceConfigurationState.Username);
                LsaUtility.SetRight(serviceConfigurationState.Username!, "SeServiceLogonRight");

                var query = new ManagementPath($"Win32_Service.Name='{thisServiceName.Replace("'", "\\'")}'");
                using (var service = new ManagementObject(query))
                {
                    var wmiParams = new object[10];
                    wmiParams[5] = false; //interact with desktop
                    wmiParams[6] = serviceConfigurationState.Username!;
                    wmiParams[7] = serviceConfigurationState.Password;
                    var result = service.InvokeMethod("Change", wmiParams);
                    if ((uint)result != 0)
                        log.Error($"Unable to set username/password on service '{thisServiceName}'. WMI returned {result}.");
                }
            }
            else
            {
                Sc($"config \"{thisServiceName}\" obj= \"{serviceConfigurationState.Username}\"");
            }

            log.Info("Service credentials set");
        }

        void ReconfigureService(string thisServiceName,
            string exePath,
            string? instance,
            string? configPath,
            string serviceDescription,
            List<string> serviceDependencies)
        {
            var instanceIdentifier = InstanceIdentifier(instance, configPath);
            var command = exePath.EndsWith(".dll")
                ? $"config \"{thisServiceName}\" binpath= \"dotnet \\\"{exePath}\\\" run {instanceIdentifier} DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto"
                : $"config \"{thisServiceName}\" binpath= \"\\\"{exePath}\\\" run {instanceIdentifier} DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto";
            Sc(command);
            Sc($"description \"{thisServiceName}\" \"{serviceDescription}\"");

            log.Info("Service reconfigured");
        }

        ServiceController? InstallService(string thisServiceName,
            string exePath,
            string? instance,
            string? configPath,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState,
            ServiceController? controller,
            List<string> serviceDependencies)
        {
            if (controller != null)
            {
                logFileOnlyLogger.Info($"Install requested for service {thisServiceName}, but service controller already existing. Triggering 'Reconfigure' mode.");
                serviceConfigurationState.Reconfigure = true;
            }
            else
            {
                var instanceIdentifier = InstanceIdentifier(instance, configPath);
                var command = exePath.EndsWith(".dll")
                    ? $"create \"{thisServiceName}\" binpath= \"dotnet \\\"{exePath}\\\" run {instanceIdentifier} DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto"
                    : $"create \"{thisServiceName}\" binpath= \"\\\"{exePath}\\\" run {instanceIdentifier} DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto";

                Sc(command);
                Sc($"description \"{thisServiceName}\" \"{serviceDescription}\"");
            }

            log.Info("Service installed");

            // Reload after install
            controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);
            return controller;
        }

        static string InstanceIdentifier(string? instance, string? configPath)
        {
            if (!string.IsNullOrEmpty(instance))
            {
                return $"--instance=\\\"{instance}\\\"\"";
            }

            if (!string.IsNullOrEmpty(configPath))
            {
                return $"--config=\\\"{configPath}\\\"\"";
            }

            throw new InvalidOperationException("Either the instance name of configuration path must be provided to configure a service");
        }

        void UninstallService(string thisServiceName, ServiceController? controller)
        {
            if (controller == null)
            {
                logFileOnlyLogger.Info($"Uninstall requested for service {thisServiceName}, but service controller was not found. Skipping.");
            }
            else
            {
                Sc($"delete \"{thisServiceName}\"");

                log.Info("Service uninstalled");
            }
        }

        void StopService(string thisServiceName, ServiceController? controller)
        {
            if (controller == null)
                logFileOnlyLogger.Info($"Stop requested for service {thisServiceName}, but service controller was not found. Skipping.");
            else
                TryStopService(controller);
        }

        void RestartService(string thisServiceName, ServiceController? controller)
        {
            if (controller == null)
            {
                logFileOnlyLogger.Info($"Restart requested for service {thisServiceName}, but service controller was not found. Skipping.");
            }
            else
            {
                log.Info($"Restarting service {thisServiceName}");
                if (TryStopService(controller))
                    StartService(thisServiceName, controller);
                else
                    log.Info($"Service {thisServiceName} wasn't restarted as it is not running.");
            }
        }

        bool TryStopService(ServiceController controller)
        {
            if (controller.Status != ServiceControllerStatus.Stopped && controller.Status != ServiceControllerStatus.StopPending)
            {
                if (!controller.CanStop)
                    try
                    {
                        WaitForControllerToBeReadyToStop(controller);
                    }
                    catch (Exception)
                    {
                        log.Error("The service is not able to stop");
                        throw;
                    }

                log.Info("Stopping service...");
                Thread.Sleep(300);
                controller.Stop();
            }
            else
            {
                return false;
            }

            try
            {
                WaitForControllerStatus(controller, ServiceControllerStatus.Stopped);
            }
            catch (Exception)
            {
                log.Error("The service could not be stopped");
                return false;
            }

            log.Info("Service stopped");

            return true;
        }

        void StartService(string thisServiceName, ServiceController? controller)
        {
            if (controller == null)
                throw new ControlledFailureException($"Start requested for service {thisServiceName}, but no service with this name was found.");

            if (controller.Status != ServiceControllerStatus.Running)
            {
                if (controller.Status != ServiceControllerStatus.StartPending)
                {
                    Policy
                        .Handle<Exception>()
                        .WaitAndRetry(5, i => TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), (_, span) => log.Warn($"Failed to start the windows service. Trying again in...{span} "))
                        .Execute(
                            () =>
                            {
                                controller.Start();
                            });
                }

                WaitForControllerStatus(controller, ServiceControllerStatus.Running);

                log.Info("Service started");
            }
        }

        void WaitForControllerToBeReadyToStop(ServiceController controller)
        {
            Retry(() =>
                {
                    controller.Refresh();
                    log.Info("Waiting for the service to be ready to stop...");
                    Thread.Sleep(300);
                    return controller.CanStop;
                },
                150);
        }

        void WaitForControllerStatus(ServiceController controller, ServiceControllerStatus status)
        {
            Retry(() =>
                {
                    controller.Refresh();
                    log.Info($"Waiting for service to become {status}. Current status: {controller.Status}");
                    Thread.Sleep(300);
                    return controller.Status == status;
                },
                150);
        }

        void Sc(string arguments)
        {
            var outputBuilder = new StringBuilder();
            var argumentsToLog = string.Join(" ", arguments);

            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var sc = Path.Combine(system32, "sc.exe");

            logFileOnlyLogger.Info($"Executing sc.exe {argumentsToLog}");
            var exitCode = SilentProcessRunner.ExecuteCommand(sc,
                arguments,
                Environment.CurrentDirectory,
                output => outputBuilder.AppendLine(output),
                error => outputBuilder.AppendLine("Error: " + error));
            if (exitCode == 0)
                logFileOnlyLogger.Info(outputBuilder.ToString());
            else
                log.Error(outputBuilder.ToString());
        }

        void Retry(Func<bool> func, int maxRetries)
        {
            var currentRetry = 0;
            while (!func())
                if (currentRetry++ > maxRetries)
                    throw new Exception("Exceeded max retries");
        }
    }
}
