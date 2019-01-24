using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Startup
{
    class NoninteractiveHost : ICommandHost, ICommandRuntime
    {
        ManualResetEvent mre = new ManualResetEvent(false); 
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
