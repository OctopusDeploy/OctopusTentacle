using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.CoreUtilities.Extensions;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
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

        public void ConfigureService(string thisServiceName,
            string exePath,
            string workingDir,
            string? instance,
            string serviceDescription,
            ServiceConfigurationState serviceConfigurationState)
        {
            if (string.IsNullOrEmpty(instance))
            {
                throw new InvalidOperationException("Only instances registered via the standard `--instance` mechanism can be run as a Windows Service");
            }

            windowsLocalAdminRightsChecker.AssertIsRunningElevated();
            var services = ServiceController.GetServices();
            var controller = services.FirstOrDefault(s => s.ServiceName == thisServiceName);

            if (serviceConfigurationState.Restart)
            {
                if (controller == null)
                {
                    logFileOnlyLogger.Info($"Restart requested for service {thisServiceName}, but service controller was not found. Skipping.");
                }
                else
                {
                    log.Info($"Restarting service {thisServiceName}");
                    if (TryStopService(controller))
                        StartService(controller);
                    else
                        log.Info($"Service {thisServiceName} wasn't restarted as it is not running.");
                }
            }

            if (serviceConfigurationState.Stop)
            {
                if (controller == null)
                    logFileOnlyLogger.Info($"Stop requested for service {thisServiceName}, but service controller was not found. Skipping.");
                else
                    TryStopService(controller);
            }

            if (serviceConfigurationState.Uninstall)
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

            var serviceDependencies = new List<string>();
            serviceDependencies.AddRange(new[] { "LanmanWorkstation", "TCPIP" });

            if (!string.IsNullOrWhiteSpace(serviceConfigurationState.DependOn))
            {
                if (services.None(x => string.Equals(x.ServiceName, serviceConfigurationState.DependOn, StringComparison.OrdinalIgnoreCase)))
                    throw new ControlledFailureException($"Unable to set dependency on service '{serviceConfigurationState.DependOn}' as no service was found with that name.");
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }

            if (serviceConfigurationState.Install)
            {
                if (controller != null)
                {
                    logFileOnlyLogger.Info($"Install requested for service {thisServiceName}, but service controller already existing. Triggering 'Reconfigure' mode.");
                    serviceConfigurationState.Reconfigure = true;
                }
                else
                {
                    var command = exePath.EndsWith(".dll")
                        ? $"create \"{thisServiceName}\" binpath= \"dotnet \\\"{exePath}\\\" run --instance=\\\"{instance}\\\"\" DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto"
                        : $"create \"{thisServiceName}\" binpath= \"\\\"{exePath}\\\" run --instance=\\\"{instance}\\\"\" DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto";

                    Sc(command);
                    Sc($"description \"{thisServiceName}\" \"{serviceDescription}\"");
                }

                log.Info("Service installed");

                // Reload after install
                controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);
            }

            if (serviceConfigurationState.Reconfigure)
            {
                var command = exePath.EndsWith(".dll")
                    ? $"config \"{thisServiceName}\" binpath= \"dotnet \\\"{exePath}\\\" run --instance=\\\"{instance}\\\"\" DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto"
                    : $"config \"{thisServiceName}\" binpath= \"\\\"{exePath}\\\" run --instance=\\\"{instance}\\\"\" DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto";
                Sc(command);
                Sc($"description \"{thisServiceName}\" \"{serviceDescription}\"");

                log.Info("Service reconfigured");
            }

            if ((serviceConfigurationState.Install || serviceConfigurationState.Reconfigure) && !string.IsNullOrWhiteSpace(serviceConfigurationState.Username))
            {
                if (!string.IsNullOrWhiteSpace(serviceConfigurationState.Password))
                {
                    log.Info("Granting log on as a service right to " + serviceConfigurationState.Username);
                    LsaUtility.SetRight(serviceConfigurationState.Username, "SeServiceLogonRight");

                    var query = new ManagementPath($"Win32_Service.Name='{thisServiceName.Replace("'", "\\'")}'");
                    using (var service = new ManagementObject(query))
                    {
                        var wmiParams = new object[10];
                        wmiParams[5] = false; //interact with desktop
                        wmiParams[6] = serviceConfigurationState.Username;
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

            if (serviceConfigurationState.Start)
            {
                if (controller == null)
                    throw new ControlledFailureException($"Start requested for service {thisServiceName}, but no service with this name was found.");

                StartService(controller);
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

        void StartService(ServiceController controller)
        {
            if (controller.Status != ServiceControllerStatus.Running)
            {
                if (controller.Status != ServiceControllerStatus.StartPending)
                    controller.Start();

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

            logFileOnlyLogger.Info($"Executing sc.exe {argumentsToLog}");
            var exitCode = SilentProcessRunner.ExecuteCommand("sc.exe",
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
