using System;
using System.Threading;

namespace Octopus.Shared.Startup
{
    class NoninteractiveHost : ICommandHost, ICommandRuntime
    {
        readonly ManualResetEvent mre = new ManualResetEvent(false);

        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            start(this);
            mre.WaitOne();
        }

        public void Stop(Action shutdown)
        {
            shutdown();
            mre.Set();
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