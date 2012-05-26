using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class WindowsServiceHost : ServiceBase
    {
        readonly Action execute;
        Thread workerThread;

        public WindowsServiceHost(Action execute)
        {
            this.execute = execute;
        }

        public void Start()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            // Sometimes a server might be under load after rebooting, or virus scanners might be busy.
            // A service will usually fail to start after 30 seconds, so by requesting additional time 
            // we can be more likely to start up successfully. Also, 120 seconds seems to be about the 
            // maximum time we can ask for.
            RequestAdditionalTime(120000);

            if (args.Length > 0 && args[0].ToLowerInvariant().Contains("debug"))
            {
                Debugger.Launch();
            }

            workerThread = new Thread(RunService);
            workerThread.Name = "";
            workerThread.Start();
        }

        void RunService()
        {
            try
            {
                execute();
            }
            catch (Exception ex)
            {
                Logger.Default.Error(ex);
            }
        }

        protected override void OnStop()
        {
            workerThread.Abort();
        }
    }
}