using System;
using System.Threading;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract class IntegrationTest
    {
        static IntegrationTest()
        {
            ThreadPool.SetMaxThreads(2000, 2000);
            ThreadPool.SetMinThreads(2000, 2000);
        }

        public static int TimeoutInMilliseconds = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;

        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource(TimeoutInMilliseconds);
            CancellationToken = cancellationTokenSource.Token;
            CancellationToken.Register(() =>
            {
                Assert.Fail("The test timed out.");
            });
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Information("Tearing down");
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }

    public class IntegrationTestTimeout : TimeoutAttribute
    {
        public IntegrationTestTimeout() : base(IntegrationTest.TimeoutInMilliseconds)
        {
        }
    }
}
