using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class CheckServicesCommand : AbstractCommand
    {
        readonly ISystemLog log;
        readonly IApplicationInstanceStore instanceLocator;
        readonly ApplicationName applicationName;
        internal HashSet<string>? instances;
        readonly IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;

        public CheckServicesCommand(ISystemLog log,
            IApplicationInstanceStore instanceLocator,
            ApplicationName applicationName,
            IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(logFileOnlyLogger)
        {
            this.log = log;
            this.instanceLocator = instanceLocator;
            this.applicationName = applicationName;
            this.windowsLocalAdminRightsChecker = windowsLocalAdminRightsChecker;

            Options.Add("instances=",
                "Comma-separated list of instances to check, or * to check all instances",
                v =>
                {
                    instances = new HashSet<string>(v.Split(',', ';'));
                });
        }

        protected override void Start()
        {
            if (instances == null)
                throw new ControlledFailureException("Use --instances argument to specify which instances to check. Use --instances=* to check all instances.");
            if (!PlatformDetection.IsRunningOnWindows)
                throw new ControlledFailureException("This command is only supported on Windows.");

            windowsLocalAdminRightsChecker.AssertIsRunningElevated();

            var startAll = instances.Count == 1 && instances.First() == "*";
            var serviceControllers = ServiceController.GetServices();
            try
            {
                foreach (var instance in instanceLocator.ListInstances())
                {
                    if (!startAll && instances.Contains(instance.InstanceName) == false)
                        continue;

                    var serviceName = ServiceName.GetWindowsServiceName(applicationName, instance.InstanceName);

                    var controller = serviceControllers.FirstOrDefault(s => s.ServiceName == serviceName);

                    if (controller != null &&
                        controller.Status != ServiceControllerStatus.Running &&
                        controller.Status != ServiceControllerStatus.StartPending)
                        try
                        {
                            controller.Start();
                            log.Info($"Service {serviceName} starting");

                            var waitUntil = DateTime.Now.AddSeconds(30);
                            while (controller.Status != ServiceControllerStatus.Running && DateTime.Now < waitUntil)
                            {
                                controller.Refresh();

                                log.Info("Waiting for service to start. Current status: " + controller.Status);
                                Thread.Sleep(300);
                            }

                            if (controller.Status == ServiceControllerStatus.Running)
                                log.Info($"Service {serviceName} started");
                            else
                                log.Info($"Service {serviceName} doesn't have Running status after 30sec. Status will be assessed again at the time of the next scheduled check.");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Service {serviceName} could not be started - {ex}");
                        }
                }
            }
            finally
            {
                foreach (var controller in serviceControllers)
                    controller.Dispose();
            }
        }
    }
}
