using System;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Startup
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class WindowsServiceHost : ICommandHost, ICommandRuntime
    {
        readonly ISystemLog log;

        public WindowsServiceHost(ISystemLog log)
        {
            this.log = log;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            log.Trace("Creating the Windows Service host adapter");

            var startService = new Action(delegate
            {
                log.Trace("Starting the Windows Service");
                start(this);
                log.Info("The Windows Service has started");
            });

            var adapter = new WindowsServiceAdapter(startService, () => Stop(shutdown), log);

            log.Trace("Running the service host adapter");
            ServiceBase.Run(adapter);
        }

        public void Stop(Action shutdown)
        {
            log.Info("Stopping the Windows Service");
            shutdown();
            log.Info("The Windows Service has stopped");
        }

        public void OnExit(int exitCode)
        {
            // Only applicable for interactive hosts
        }

        public void WaitForUserToExit()
        {
            // Only applicable for interactive hosts; services are stopped via the service control panel
        }
    }
}