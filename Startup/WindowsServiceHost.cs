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
        readonly Action shutdown;
        Thread workerThread;

        public WindowsServiceHost(Action execute, Action shutdown)
        {
            this.execute = execute;
            this.shutdown = shutdown;
        }

        public void Start()
        {
            Run(this);
        }

        protected override void OnStart(string[] args)
        {
            if (args.Length > 0 && args[0].ToLowerInvariant().Contains("debug"))
            {
                Debugger.Launch();
            }

            // Sometimes a server might be under load after rebooting, or virus scanners might be busy.
            // A service will usually fail to start after 30 seconds, so by requesting additional time 
            // we can be more likely to start up successfully. Also, 120 seconds seems to be about the 
            // maximum time we can ask for.
            RequestAdditionalTime(120000);

            workerThread = new Thread(RunService);
            workerThread.IsBackground = true;
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
                LogAdapter.GetDefault().Error(ex);
                throw;
            }
        }

        protected override void OnStop()
        {
            shutdown();
        }
    }
}