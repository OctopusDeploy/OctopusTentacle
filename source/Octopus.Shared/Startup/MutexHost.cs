using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Startup
{
    class MutexHost : ICommandHost, ICommandRuntime
    {
        private readonly string monitorMutexHost;
        private readonly CancellationTokenSource sourceToken = new CancellationTokenSource();
        private Task task;

        public MutexHost(string monitorMutexHost)
        {
            this.monitorMutexHost = monitorMutexHost;
        }

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            if (Mutex.TryOpenExisting(monitorMutexHost, out var m))
            {
                task = Task.Run(() =>
                {
                    while (!sourceToken.IsCancellationRequested)
                    {
                        if (m.WaitOne(500))
                        {
                            shutdown();
                            break;
                        }
                    }
                });
            }
            
            start(this);
        }

        public void Stop(Action shutdown)
        {
            sourceToken.Cancel();
            task?.Wait();
            
            shutdown();
        }

        public void OnExit(int exitCode)
        {
            // Only applicable for interactive hosts
        }

        public void WaitForUserToExit()
        {
            // Only applicable for interactive hosts; stop this with a docker or kubectl command
        }
    }
}