using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest
    {
        CancellationTokenSource? cancellationTokenSource;
        private CancellationTokenRegistration? cancellationTokenRegistration;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource(IntegrationTestTimeout.TestTimeoutInMilliseconds() - (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            CancellationToken = cancellationTokenSource.Token;
            cancellationTokenRegistration = CancellationToken.Register(() =>
            {
                Logger.Error("The test timed out.");
                Assert.Fail("The test timed out.");
            });
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Information("Tearing down");
            
            cancellationTokenRegistration?.Dispose();
            cancellationTokenSource?.Dispose();
            
        }
    }

    public class IntegrationTestTimeout : TimeoutAttribute
    {
        public IntegrationTestTimeout(int timeoutInSeconds) : base((int)TimeSpan.FromSeconds(timeoutInSeconds).TotalMilliseconds)
        {
        }

        public IntegrationTestTimeout() : base(TestTimeoutInMilliseconds())
        {
        }

        public static int TestTimeoutInMilliseconds()
        {
            if (Debugger.IsAttached)
            {
                return (int)TimeSpan.FromHours(1).TotalMilliseconds;
            }

            return (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
        }
    }
}
