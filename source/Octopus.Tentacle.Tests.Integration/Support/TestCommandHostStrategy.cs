using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class TestCommandHostStrategy : ICommandHostStrategy
    {
        private readonly CancellationToken token;

        public TestCommandHostStrategy(CancellationToken token)
        {
            this.token = token;
        }
        
        public ICommandHost SelectMostAppropriateHost(ICommand command, string displayName, ISystemLog log, bool forceConsoleHost, bool forceNoninteractiveHost, string? monitorMutexHost)
        {
            return new TestCommandHost(token);
        }
    }

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
                throw new Exception("ARGHH");
            }
        }

        public void WaitForUserToExit()
        {
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}
