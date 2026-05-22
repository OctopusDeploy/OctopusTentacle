using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Startup
{
    class NoninteractiveHost : ICommandHost, ICommandRuntime
    {
        readonly ManualResetEvent mre = new ManualResetEvent(false);
        readonly TaskCompletionSource<bool> stopTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            start(this);
            mre.WaitOne();
        }

        public async Task RunAsync(Func<ICommandRuntime, Task> start, Action shutdown)
        {
            await start(this);
            await stopTcs.Task;
        }

        public void Stop(Action shutdown)
        {
            shutdown();
            mre.Set();
            stopTcs.TrySetResult(true);
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
