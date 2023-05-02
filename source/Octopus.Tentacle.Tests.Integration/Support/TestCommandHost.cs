using System;
using System.Threading;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TestCommandHost : ICommandHost, ICommandRuntime
    {
        private readonly CancellationToken cancellationToken;

        public TestCommandHost(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }
        
        public void Run(Action<ICommandRuntime> start, Action shutdown)
        {
            start(this);
            Stop(shutdown);
        }

        public void Stop(Action shutdown)
        {
            shutdown();
        }

        public void OnExit(int exitCode)
        {
            if (exitCode != 0)
            {
                throw new Exception("We got a non-zero exit code! Time to PANIC!! Yeah nah, it's just a test, it's cool.");
            }
        }

        public void WaitForUserToExit()
        {
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}
