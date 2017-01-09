using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class CheckServicesCommand : AbstractCommand
    {
        readonly ILog log;
        HashSet<string> instances;
        readonly IApplicationInstanceStore applicationInstanceStore;
        readonly ApplicationName applicationName;

        public CheckServicesCommand(ILog log,
            IApplicationInstanceStore applicationInstanceStore,
            ApplicationName applicationName)
        {
            this.log = log;
            this.applicationInstanceStore = applicationInstanceStore;
            this.applicationName = applicationName;

            Options.Add("instances=", "List of instances to check", v =>
            {
                instances = new HashSet<string>(v.Split(',', ';'));
            });
        }

        protected override void Start()
        {
            var startAll = instances.Count == 1 && instances.First() == "*";

            foreach (var instance in applicationInstanceStore.ListInstances(applicationName))
            {
                if (!startAll && instances.Contains(instance.InstanceName) == false)
                    continue;

                var serviceName = ServiceName.GetWindowsServiceName(applicationName, instance.InstanceName);
                using (var controller = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName))
                {
                    if (controller != null &&
                        controller.Status != ServiceControllerStatus.Running &&
                        controller.Status != ServiceControllerStatus.StartPending)
                    {
                        try
                        {
                            controller.Start();

                            while (controller.Status != ServiceControllerStatus.Running)
                            {
                                controller.Refresh();

                                log.Info("Waiting for service to start. Current status: " + controller.Status);
                                Thread.Sleep(300);
                            }

                            log.Info($"Service {serviceName} started");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Service {serviceName} could not be started - {ex}");
                        }
                    }
                }
            }
        }
    }
}