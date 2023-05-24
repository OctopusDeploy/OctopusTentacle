using System;
using System.Threading;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public abstract class IntegrationTest : IDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource;
        public CancellationToken CancellationToken { get; }

        protected IntegrationTest()
        {
            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            CancellationToken = cancellationTokenSource.Token;
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }
    }
}