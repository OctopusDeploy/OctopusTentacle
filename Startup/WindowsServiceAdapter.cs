using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Diagnostics.KnowledgeBase;

namespace Octopus.Shared.Startup
{
    public class WindowsServiceAdapter : ServiceBase
    {
        readonly Action execute;
        readonly Action shutdown;
        Thread workerThread;

        public WindowsServiceAdapter(Action execute, Action shutdown)
        {
            this.execute = execute;
            this.shutdown = shutdown;
        }

        protected override void OnStart(string[] args)
        {
            Log.Octopus().Trace("WindowsServiceAdapter.OnStart()");
            if (args.Length > 0 && args[0].ToLowerInvariant().Contains("debug"))
            {
                Debugger.Launch();
            }

            // Sometimes a server might be under load after rebooting, or virus scanners might be busy.
            // A service will usually fail to start after 30 seconds, so by requesting additional time 
            // we can be more likely to start up successfully. Also, 120 seconds seems to be about the 
            // maximum time we can ask for.
            Log.Octopus().Trace("WindowsServiceAdapter.OnStart() : Requesting an additional 120 seconds for startup");
            RequestAdditionalTime(120000);

            workerThread = new Thread(RunService);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        void RunService()
        {
            try
            {
                Log.Octopus().Trace("WindowsServiceAdapter.RunService() : executing");

                execute();
                Log.Octopus().Trace("WindowsServiceAdapter.RunService() : execute complete");
            }
            catch (Exception ex)
            {
                ExceptionKnowledgeBaseEntry entry;
                if (ExceptionKnowledgeBase.TryInterpret(ex, out entry))
                {
                    var message = entry.ToString();
                    Log.Octopus().Error(ex, message);
                    throw new Exception(message, ex) {HelpLink = entry.HelpLink};
                }

                Log.Octopus().Fatal(ex);
                Log.Octopus().Flush();

                throw;
            }
        }

        protected override void OnStop()
        {
            shutdown();
        }
    }
}