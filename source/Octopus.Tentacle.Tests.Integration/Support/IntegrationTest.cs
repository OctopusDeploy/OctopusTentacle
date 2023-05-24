using System;
using System.Threading;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public abstract class IntegrationTest : IDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource;
        public CancellationToken CancellationToken { get; }
        public ILogger Logger { get; }

        protected IntegrationTest()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            CancellationToken = cancellationTokenSource.Token;
            Logger = new SerilogLoggerBuilder().Build();
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }
    }
}
