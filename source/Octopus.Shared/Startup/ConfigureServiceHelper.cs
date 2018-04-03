using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class ConfigureServiceHelper
    {
        readonly ILog log;
        readonly string thisServiceName;
        readonly string exePath;
        readonly string instance;
        readonly string serviceDescription;
        readonly ServiceConfigurationState serviceConfigurationState;

        public ConfigureServiceHelper(ILog log, string thisServiceName, string exePath, string instance, string serviceDescription, ServiceConfigurationState serviceConfigurationState)
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
            var controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);

            if (serviceConfigurationState.Stop)
            {
                if (controller == null)
                {
                    LogFileOnlyLogger.Info($"Stop requested for service {thisServiceName}, but service controller was not found. Skipping.");
                }
                else
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

                    try
                    {
                        WaitForControllerStatus(controller, ServiceControllerStatus.Stopped);
                    }
                    catch (Exception)
                    {
                        log.Error("The service could not be stopped");
                        throw;
                    }

                    log.Info("Service stopped");
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
                    Sc(
                        string.Format(
                            "delete \"{0}\"",
                            thisServiceName
                            ));

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

                    Sc(
                        string.Format(
                            "description \"{0}\" \"{1}\"",
                            thisServiceName,
                            serviceDescription
                            ));
                }

                log.Info("Service installed");

                // Reload after install
                controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);
            }

            if (serviceConfigurationState.Reconfigure)
            {
                Sc(
                    string.Format(
                        "config \"{0}\" binpath= \"\\\"{1}\\\" run --instance=\\\"{2}\\\"\" DisplayName= \"{0}\" depend= {3} start= auto",
                        thisServiceName,
                        exePath,
                        instance,
                        string.Join("/", serviceDependencies)
                        ));

                Sc(
                    string.Format(
                        "description \"{0}\" \"{1}\"",
                        thisServiceName,
                        serviceDescription
                        ));

                log.Info("Service reconfigured");
            }

            if ((serviceConfigurationState.Install || serviceConfigurationState.Reconfigure) && !string.IsNullOrWhiteSpace(serviceConfigurationState.Username))
            {
                if (!string.IsNullOrWhiteSpace(serviceConfigurationState.Password))
                {
                    log.Info("Granting log on as a service right to " + serviceConfigurationState.Username);
                    LsaUtility.SetRight(serviceConfigurationState.Username, "SeServiceLogonRight");

                    Sc(
                        string.Format(
                            "config \"{0}\" obj= \"{1}\" password= \"{2}\"",
                            thisServiceName, serviceConfigurationState.Username, serviceConfigurationState.Password
                            ));
                }
                else
                {
                    Sc(
                        string.Format(
                            "config \"{0}\" obj= \"{1}\"",
                            thisServiceName, serviceConfigurationState.Username
                            ));
                }

                log.Info("Service credentials set");
            }

            if (serviceConfigurationState.Start)
            {
                if (controller == null)
                {
                    throw new ControlledFailureException($"Start requested for service {thisServiceName}, but no service with this name was found.");
                }

                if (controller.Status != ServiceControllerStatus.Running)
                {
                    if (controller.Status != ServiceControllerStatus.StartPending)
                    {
                        controller.Start();
                    }

                    while (controller.Status != ServiceControllerStatus.Running)
                    {
                        controller.Refresh();

                        log.Info("Waiting for service to start. Current status: " + controller.Status);
                        Thread.Sleep(300);
                    }

                    log.Info("Service started");
                }
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
