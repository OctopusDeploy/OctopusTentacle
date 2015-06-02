using System;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.Shared.Diagnostics;
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
                if (controller != null)
                {
                    if (controller.Status != ServiceControllerStatus.Stopped && controller.Status != ServiceControllerStatus.StopPending)
                    {
                        while (!controller.CanStop)
                        {
                            controller.Refresh();
                            log.Info("Waiting for the service to be ready to stop...");
                            Thread.Sleep(300);
                        }

                        log.Info("Stopping service...");
                        Thread.Sleep(300);
                        controller.Stop();
                    }

                    while (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Refresh();

                        log.Info("Waiting for service to stop. Current status: " + controller.Status);
                        Thread.Sleep(300);
                    }

                    log.Info("Service stopped");
                }
            }

            if (serviceConfigurationState.Uninstall)
            {
                if (controller != null)
                {
                    Sc(
                        string.Format(
                            "delete \"{0}\"",
                            thisServiceName
                            ));

                    log.Info("Service uninstalled");
                }
            }

            if (serviceConfigurationState.Install)
            {
                if (controller != null)
                {
                    serviceConfigurationState.Reconfigure = true;
                }
                else
                {
                    Sc(
                        string.Format(
                            "create \"{0}\" binpath= \"\\\"{1}\\\" run --instance=\\\"{2}\\\"\" DisplayName= \"{0}\" depend= LanmanWorkstation/TCPIP start= auto",
                            thisServiceName,
                            exePath,
                            instance
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
                        "config \"{0}\" binpath= \"\\\"{1}\\\" run --instance=\\\"{2}\\\"\" DisplayName= \"{0}\" depend= LanmanWorkstation/TCPIP start= auto",
                        thisServiceName,
                        exePath,
                        instance
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
                    return;

                if (controller.Status != ServiceControllerStatus.Running && controller.Status != ServiceControllerStatus.StartPending)
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

        void Sc(string arguments)
        {
            var outputBuilder = new StringBuilder();
            var exitCode = SilentProcessRunner.ExecuteCommand("sc.exe", arguments, Environment.CurrentDirectory, output => outputBuilder.AppendLine(output), error => outputBuilder.AppendLine("Error: " + error));
            if (exitCode != 0)
            {
                log.Error(outputBuilder.ToString());
            }
        }
    }
}
