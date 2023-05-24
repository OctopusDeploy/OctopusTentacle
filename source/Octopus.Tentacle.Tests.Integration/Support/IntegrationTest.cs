using System.Threading;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public abstract class IntegrationTest
    {
        public CancellationToken CancellationToken { get; }
        public ILogger Logger { get; }

        protected IntegrationTest()
        {
            CancellationToken = TestCancellationToken.Token();
            Logger = new SerilogLoggerBuilder().Build();
        }
    }
}
