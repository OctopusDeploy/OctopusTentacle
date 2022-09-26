using System;
using System.Threading;

namespace Octopus.Tentacle.Startup
{
    internal class NoninteractiveHost : ICommandHost, ICommandRuntime
    {
        private readonly ManualResetEvent mre = new(false);

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