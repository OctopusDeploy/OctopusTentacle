using System;
using System.ServiceProcess;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class WindowsServiceHost : ICommandHost, ICommandRuntime
    {
        readonly ILog log = Log.Octopus();

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            log.Trace("Creating the Windows Service host adapter");

            var startService = new Action(delegate
            {
                log.Trace("Starting the Windows Service");
                start(this);
                log.Info("The Windows Service has started");
            });

            var stopService = new Action(delegate
            {
                log.Info("Stopping the Windows Service");
                shutdown();
                log.Info("The Windows Service has stopped");
            });

            var adapter = new WindowsServiceAdapter(startService, stopService);

            log.Trace("Running the service host adapter");
            ServiceBase.Run(adapter);
        }

        public void WaitForUserToExit()
        {
            // Only applicable for interactive hosts; services are stopped via the service control panel
        }
    }
}