using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics.KnowledgeBase;

namespace Octopus.Tentacle.Startup
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class WindowsServiceAdapter : ServiceBase
    {
        readonly Action execute;
        readonly Action shutdown;
        readonly ISystemLog systemLog;
        Thread? workerThread;

        public WindowsServiceAdapter(Action execute, Action shutdown, ISystemLog systemLog)
        {
            this.execute = execute;
            this.shutdown = shutdown;
            this.systemLog = systemLog;
        }

        protected override void OnStart(string[] args)
        {
            if (args.Length > 0 && args[0].ToLowerInvariant().Contains("debug"))
                Debugger.Launch();

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
                if (ExceptionKnowledgeBase.TryInterpret(ex, out var entry) && entry != null)
                {
                    var message = entry.ToString();
                    systemLog.Error(ex, message);
                    throw new Exception(message, ex) { HelpLink = entry.HelpLink };
                }

                systemLog.Fatal(ex);
                systemLog.Flush();

                throw;
            }
        }

        protected override void OnStop()
        {
            shutdown();
        }
    }
}