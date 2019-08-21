using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class WindowsServiceConfigurator : IServiceConfigurator
    {
        readonly ILog log;

        public WindowsServiceConfigurator(ILog log)
        {
            this.log = log;
        }

        public void ConfigureService(string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
        {
            var controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);

            if (serviceConfigurationState.Restart)
            {
                if (controller == null)
                {
                    LogFileOnlyLogger.Info($"Restart requested for service {thisServiceName}, but service controller was not found. Skipping.");
                }
                else
                {
                    log.Info($"Restarting service {thisServiceName}");
                    if (TryStopService(controller))
                    {
                        StartService(controller);
                    }
                    else
                    {
                        log.Info($"Service {thisServiceName} wasn't restarted as it is not running.");
                    }
                }
            }

            if (serviceConfigurationState.Stop)
            {
                if (controller == null)
                {
                    LogFileOnlyLogger.Info($"Stop requested for service {thisServiceName}, but service controller was not found. Skipping.");
                }
                else
                {
                    TryStopService(controller);
                }
            }

            if (serviceConfigurationState.Uninstall)
            {
                if (controller == null)
                {
                    LogFileOnlyLogger.Info($"Uninstall requested for service {thisServiceName}, but service controller was not found. Skipping.");
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
                serviceDependencies.Add(serviceConfigurationState.DependOn);
            }

            if (serviceConfigurationState.Install)
            {
                if (controller != null)
                {
                    LogFileOnlyLogger.Info($"Install requested for service {thisServiceName}, but service controller already existing. Triggering 'Reconfigure' mode.");
                    serviceConfigurationState.Reconfigure = true;
                }
                else
                {
                    Sc(
                        string.Format(
                            "create \"{0}\" binpath= \"\\\"{1}\\\" run --instance=\\\"{2}\\\"\" DisplayName= \"{0}\" depend= {3} start= auto",
                            thisServiceName,
                            exePath,
                            instance,
                            string.Join("/", serviceDependencies)
                            ));

                    Sc($"description \"{thisServiceName}\" \"{serviceDescription}\"");
                }

                log.Info("Service installed");

                // Reload after install
                controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);
            }

            if (serviceConfigurationState.Reconfigure)
            {
                Sc($"config \"{thisServiceName}\" binpath= \"\\\"{exePath}\\\" run --instance=\\\"{instance}\\\"\" DisplayName= \"{thisServiceName}\" depend= {string.Join("/", serviceDependencies)} start= auto");

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
                {
                    throw new ControlledFailureException($"Start requested for service {thisServiceName}, but no service with this name was found.");
                }

                StartService(controller);
            }
        }

        bool TryStopService(ServiceController controller)
        {

            if (controller.Status != ServiceControllerStatus.Stopped && controller.Status != ServiceControllerStatus.StopPending)
            {
                if (!controller.CanStop)
                {
                    try
                    {
                        WaitForControllerToBeReadyToStop(controller);
                    }
                    catch (Exception)
                    {
                        log.Error("The service is not able to stop");
                        throw;
                    }
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
                {
                    controller.Start();
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
            }, 150);
        }

        void WaitForControllerStatus(ServiceController controller, ServiceControllerStatus status)
        {
            Retry(() =>
            {
                controller.Refresh();
                log.Info($"Waiting for service to become {status}. Current status: {controller.Status}");
                Thread.Sleep(300);
                return controller.Status == status;
            }, 150);
        }

        void Sc(string arguments)
        {
            var outputBuilder = new StringBuilder();
            var argumentsToLog = string.Join(" ", arguments);

            LogFileOnlyLogger.Info($"Executing sc.exe {argumentsToLog}");
            var exitCode = SilentProcessRunner.ExecuteCommand("sc.exe", arguments, Environment.CurrentDirectory, output => outputBuilder.AppendLine(output), error => outputBuilder.AppendLine("Error: " + error));
            if (exitCode == 0)
            {
                LogFileOnlyLogger.Info(outputBuilder.ToString());
            }
            else
            {
                log.Error(outputBuilder.ToString());
            }
        }

        void Retry(Func<bool> func, int maxRetries)
        {
            var currentRetry = 0;
            while (!func())
            {
                if (currentRetry++ > maxRetries)
                {
                    throw new Exception("Exceeded max retries");
                }
            }
        }
    }
}
