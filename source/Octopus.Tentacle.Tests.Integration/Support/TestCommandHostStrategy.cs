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
}
