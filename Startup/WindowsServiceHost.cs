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