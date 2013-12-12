using System;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Security.Masking;
using Octopus.Platform.Util;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class ServiceCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        readonly string serviceDescription;
        readonly Assembly assemblyContainingService;
        readonly ILog log;
        bool start;
        bool stop;
        bool reconfigure;
        bool install;
        bool uninstall;
        string username;
        string password;

        public ServiceCommand(IApplicationInstanceSelector instanceSelector, string serviceDescription, Assembly assemblyContainingService, ILog log) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            this.serviceDescription = serviceDescription;
            this.assemblyContainingService = assemblyContainingService;
            this.log = log;

            Options.Add("start", "Start the Windows Service if it is not already running", v => start = true);
            Options.Add("stop", "Stop the Windows Service if it is running", v => stop = true);
            Options.Add("reconfigure", "Reconfigure the Windows Service", v => reconfigure = true);
            Options.Add("install", "Install the Windows Service", v => install = true);
            Options.Add("username=", "Username to run the service under (DOMAIN\\Username format). Only used when --install is used.", v => username = v);
            Options.Add("uninstall", "Uninstall the Windows Service", v => uninstall = true);
            Options.Add("password=", "Password for the username specified with --username. Only used when --install is used.", v =>
            {
                MaskingContext.Permanent.MaskInstancesOf(password);
                password = v;
            });

        }

        protected override void Start()
        {
            base.Start();

            var thisServiceName = ServiceName.GetWindowsServiceName(instanceSelector.Current.ApplicationName, instanceSelector.Current.InstanceName);
            var instance = instanceSelector.Current.InstanceName;
            var exePath = assemblyContainingService.FullLocalPath();

            var controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == thisServiceName);

            if (stop)
            {
                if (controller != null)
                {
                    if (controller.Status != ServiceControllerStatus.Stopped && controller.Status != ServiceControllerStatus.StopPending)
                    {
                        while (!controller.CanStop)
                        {
                            controller.Refresh();
                            log.Info("Waiting for the service to be ready to stop...");
                            Thread.Sleep(1000);
                        }

                        log.Info("Stopping service...");
                        Thread.Sleep(1000);
                        controller.Stop();
                    }

                    while (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Refresh();

                        log.Info("Waiting for service to stop. Current status: " + controller.Status);
                        Thread.Sleep(1000);
                    }

                    log.Info("Service stopped");
                }
            }

            if (uninstall)
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

            if (install)
            {
                if (controller != null)
                {
                    reconfigure = true;
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

            if (reconfigure)
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

            if ((install || reconfigure) && !string.IsNullOrWhiteSpace(username))
            {
                if (!string.IsNullOrWhiteSpace(password))
                {
                    log.Info("Granting log on as a service right to " + username);
                    LsaUtility.SetRight(username, "SeServiceLogonRight");

                    Sc(
                        string.Format(
                            "config \"{0}\" obj= \"{1}\" password= \"{2}\"",
                            thisServiceName,
                            username,
                            password
                            ));
                }
                else
                {
                    Sc(
                        string.Format(
                            "config \"{0}\" obj= \"{1}\"",
                            thisServiceName,
                            username
                            ));
                }

                log.Info("Service credentials set");
            }

            if (start)
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
                    Thread.Sleep(1000);
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