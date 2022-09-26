using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Startup
{
    internal class MutexHost : ICommandHost, ICommandRuntime
    {
        private static readonly int[] FriendlyExitCodes =
        {
            (int)OctopusProgram.ExitCode.Success,
            (int)OctopusProgram.ExitCode.UnknownCommand,
            (int)OctopusProgram.ExitCode.ControlledFailureException
        };

        private readonly string monitorMutexHost;
        private readonly ISystemLog log;
        private readonly CancellationTokenSource sourceToken = new();
        private readonly ManualResetEventSlim shutdownTrigger = new(false);
        private Task? task;

        public MutexHost(string monitorMutexHost, ISystemLog log)
        {
            this.monitorMutexHost = monitorMutexHost;
            this.log = log;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            if (Mutex.TryOpenExisting(monitorMutexHost, out var m))
                task = Task.Run(() =>
                {
                    while (!sourceToken.IsCancellationRequested)
                        if (m!.WaitOne(500))
                        {
                            shutdown();
                            shutdownTrigger.Set();
                            m.ReleaseMutex();
                            break;
                        }
                });

            start(this);
        }

        public void Stop(Action shutdown)
        {
            sourceToken.Cancel();
            task?.Wait();

            if (shutdownTrigger.IsSet)
                return;

            shutdown();
            shutdownTrigger.Set();
        }

        public void OnExit(int exitCode)
        {
            if (FriendlyExitCodes.Contains(exitCode)) return;

            var sb = new StringBuilder()
                .AppendLine(new string('-', 79))
                .AppendLine($"Terminating process with exit code {exitCode}")
                .AppendLine("Full error details are available in the log files at:");
            foreach (var logDirectory in OctopusLogsDirectoryRenderer.LogsDirectoryHistory)
                sb.AppendLine(logDirectory);
            sb.AppendLine("If you need help, please send these log files to https://octopus.com/support");
            sb.AppendLine(new string('-', 79));
            log.Fatal(sb.ToString());
        }

        public void WaitForUserToExit()
        {
            shutdownTrigger.Wait();
        }
    }
}